using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Api.Options;
using Domain.IdentityEntities;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Data;

namespace Api.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly UserManager<User> _userManager;
    private readonly ProductDbContext _dbContext;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        UserManager<User> userManager,
        ProductDbContext dbContext)
    {
        _options = options.Value;
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<(string Token, DateTime ExpiresAtUtc)> CreateTokenAsync(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessTokenLifetimeMinutes = Math.Clamp(_options.AccessTokenLifetimeMinutes, 5, 30);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(accessTokenLifetimeMinutes);
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (!roles.Any())
        {
            claims.Add(new Claim(ClaimTypes.Role, IdentitySeed.User));
        }

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        return (token, expiresAtUtc);
    }

    public async Task<AuthSessionTokens> CreateSessionAsync(
        User user,
        string? createdByIp,
        CancellationToken cancellationToken = default)
    {
        var (accessToken, accessTokenExpiresAtUtc) = await CreateTokenAsync(user);
        var now = DateTime.UtcNow;
        var refreshToken = CreateOpaqueToken();
        var refreshTokenExpiresAtUtc = now.AddDays(Math.Max(2, _options.RefreshTokenLifetimeDays));
        var absoluteExpiresAtUtc = now.AddDays(Math.Max(
            Math.Max(2, _options.RefreshTokenLifetimeDays),
            _options.RefreshTokenAbsoluteLifetimeDays));

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            FamilyId = Guid.NewGuid(),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshTokenExpiresAtUtc,
            AbsoluteExpiresAtUtc = absoluteExpiresAtUtc,
            CreatedByIp = NormalizeIp(createdByIp)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new AuthSessionTokens(
            user,
            accessToken,
            accessTokenExpiresAtUtc,
            refreshToken,
            refreshTokenExpiresAtUtc);
    }

    public async Task<AuthSessionTokens?> RotateRefreshTokenAsync(
        string refreshToken,
        string? rotatedByIp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var tokenHash = HashToken(refreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (storedToken.RevokedAtUtc.HasValue)
        {
            // A rotated token being reused is treated as token-family compromise.
            if (!string.IsNullOrWhiteSpace(storedToken.ReplacedByTokenHash))
            {
                await _dbContext.RefreshTokens
                    .Where(token => token.FamilyId == storedToken.FamilyId && token.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(token => token.RevokedAtUtc, now)
                        .SetProperty(token => token.RevokedByIp, NormalizeIp(rotatedByIp)), cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (storedToken.ExpiresAtUtc <= now ||
            storedToken.AbsoluteExpiresAtUtc <= now ||
            storedToken.User.IsBlocked ||
            (storedToken.User.LockoutEnd.HasValue && storedToken.User.LockoutEnd.Value > now))
        {
            storedToken.RevokedAtUtc = now;
            storedToken.RevokedByIp = NormalizeIp(rotatedByIp);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var nextRefreshToken = CreateOpaqueToken();
        var nextRefreshTokenHash = HashToken(nextRefreshToken);
        var nextExpiresAtUtc = Min(
            now.AddDays(Math.Max(2, _options.RefreshTokenLifetimeDays)),
            storedToken.AbsoluteExpiresAtUtc);

        storedToken.RevokedAtUtc = now;
        storedToken.RevokedByIp = NormalizeIp(rotatedByIp);
        storedToken.ReplacedByTokenHash = nextRefreshTokenHash;

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = storedToken.UserId,
            TokenHash = nextRefreshTokenHash,
            FamilyId = storedToken.FamilyId,
            CreatedAtUtc = now,
            ExpiresAtUtc = nextExpiresAtUtc,
            AbsoluteExpiresAtUtc = storedToken.AbsoluteExpiresAtUtc,
            CreatedByIp = NormalizeIp(rotatedByIp)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var (accessToken, accessTokenExpiresAtUtc) = await CreateTokenAsync(storedToken.User);
        return new AuthSessionTokens(
            storedToken.User,
            accessToken,
            accessTokenExpiresAtUtc,
            nextRefreshToken,
            nextExpiresAtUtc);
    }

    public async Task RevokeRefreshTokenAsync(
        string refreshToken,
        string? revokedByIp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var storedToken = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == HashToken(refreshToken), cancellationToken);
        if (storedToken is null || storedToken.RevokedAtUtc.HasValue)
        {
            return;
        }

        storedToken.RevokedAtUtc = DateTime.UtcNow;
        storedToken.RevokedByIp = NormalizeIp(revokedByIp);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string CreateOpaqueToken()
    {
        return Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string? NormalizeIp(string? ipAddress)
    {
        return string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress[..Math.Min(ipAddress.Length, 64)];
    }

    private static DateTime Min(DateTime first, DateTime second) => first <= second ? first : second;
}
