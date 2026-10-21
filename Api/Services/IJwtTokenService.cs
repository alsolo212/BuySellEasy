using Domain.IdentityEntities;

namespace Api.Services;

public interface IJwtTokenService
{
    Task<(string Token, DateTime ExpiresAtUtc)> CreateTokenAsync(User user);

    Task<AuthSessionTokens> CreateSessionAsync(
        User user,
        string? createdByIp,
        CancellationToken cancellationToken = default);

    Task<AuthSessionTokens?> RotateRefreshTokenAsync(
        string refreshToken,
        string? rotatedByIp,
        CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(
        string refreshToken,
        string? revokedByIp,
        CancellationToken cancellationToken = default);
}

public sealed record AuthSessionTokens(
    User User,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
