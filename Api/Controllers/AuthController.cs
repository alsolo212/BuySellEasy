using Api.Contracts.Auth;
using Api.Extensions;
using Api.Options;
using Api.Services;
using Application.DTO.IdentityDto;
using Domain.IdentityEntities;
using Google.Apis.Auth;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string GoogleLoginProvider = "Google";

    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly GoogleAuthOptions _googleAuthOptions;
    private readonly JwtOptions _jwtOptions;

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IJwtTokenService jwtTokenService,
        IOptions<GoogleAuthOptions> googleAuthOptions,
        IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _googleAuthOptions = googleAuthOptions.Value;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("register")]
    [EnableRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicyName)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterDTO request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            ModelState.AddModelError(nameof(request.Email), "A user with this email already exists.");
            return ValidationProblem(ModelState);
        }

        var user = new User
        {
            UserName = request.UserName,
            Email = request.Email,
            PhoneNumber = request.Phone,
            CreatedAt = DateTime.UtcNow,
            IsVerified = false,
            LockoutEnabled = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        await _userManager.AddToRoleAsync(user, IdentitySeed.User);

        var response = await BuildAuthResponseAsync(user);
        return CreatedAtAction(nameof(Me), null, response);
    }

    [HttpPost("login")]
    [EnableRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicyName)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginDTO request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || user.IsBlocked)
        {
            return InvalidCredentials();
        }

        if (!user.LockoutEnabled)
        {
            user.LockoutEnabled = true;
            var lockoutUpdateResult = await _userManager.UpdateAsync(user);
            if (!lockoutUpdateResult.Succeeded)
            {
                return InvalidCredentials();
            }
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        user.LastLogin = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return Ok(await BuildAuthResponseAsync(user));
    }

    [AllowAnonymous]
    [HttpGet("google-config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> GoogleConfiguration()
    {
        // OAuth client IDs are public by design. Secrets are never returned here.
        return Ok(new { clientId = _googleAuthOptions.ClientId });
    }

    [HttpPost("google")]
    [EnableRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicyName)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(_googleAuthOptions.ClientId))
        {
            return Problem(
                title: "Google authentication is not configured.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_googleAuthOptions.ClientId]
                });
        }
        catch (InvalidJwtException)
        {
            return InvalidCredentials();
        }

        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Subject))
        {
            return InvalidCredentials();
        }

        var user = await _userManager.FindByLoginAsync(GoogleLoginProvider, payload.Subject);
        if (user is null)
        {
            user = await _userManager.FindByEmailAsync(payload.Email);
            if (user?.IsBlocked == true)
            {
                return InvalidCredentials();
            }

            if (user is null)
            {
                user = new User
                {
                    UserName = await BuildUniqueUserNameAsync(payload.Name, payload.Email),
                    Email = payload.Email,
                    EmailConfirmed = true,
                    ProfileImageUrl = string.IsNullOrWhiteSpace(payload.Picture) ? null : payload.Picture,
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow,
                    IsVerified = false,
                    LockoutEnabled = true
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    AddIdentityErrors(createResult);
                    return ValidationProblem(ModelState);
                }

                var roleResult = await _userManager.AddToRoleAsync(user, IdentitySeed.User);
                if (!roleResult.Succeeded)
                {
                    AddIdentityErrors(roleResult);
                    return ValidationProblem(ModelState);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(user.UserName))
                {
                    user.UserName = await BuildUniqueUserNameAsync(payload.Name, payload.Email);
                }

                if (string.IsNullOrWhiteSpace(user.ProfileImageUrl) && !string.IsNullOrWhiteSpace(payload.Picture))
                {
                    user.ProfileImageUrl = payload.Picture;
                }

                user.EmailConfirmed = true;
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                AddIdentityErrors(updateResult);
                return ValidationProblem(ModelState);
            }

            var loginResult = await _userManager.AddLoginAsync(
                user,
                new UserLoginInfo(GoogleLoginProvider, payload.Subject, GoogleLoginProvider));

            if (!loginResult.Succeeded)
            {
                AddIdentityErrors(loginResult);
                return ValidationProblem(ModelState);
            }
        }

        if (user.IsBlocked)
        {
            return InvalidCredentials();
        }

        user.LastLogin = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return Ok(await BuildAuthResponseAsync(user));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicyName)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_jwtOptions.RefreshTokenCookieName];
        var session = await _jwtTokenService.RotateRefreshTokenAsync(
            refreshToken ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (session is null)
        {
            DeleteRefreshTokenCookie();
            return InvalidSession();
        }

        return Ok(await BuildAuthResponseAsync(session));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_jwtOptions.RefreshTokenCookieName];
        await _jwtTokenService.RevokeRefreshTokenAsync(
            refreshToken ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        DeleteRefreshTokenCookie();
        return NoContent();
    }

    [Authorize]
    [HttpPost("password")]
    [ProducesResponseType(typeof(UserSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserSummaryResponse>> SetPassword([FromBody] SetPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        if (user.IsBlocked)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "This account is blocked." });
        }

        if (!string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return Conflict(new { message = "A password is already configured for this account." });
        }

        var result = await _userManager.AddPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return ValidationProblem(ModelState);
        }

        return Ok(await BuildUserSummaryAsync(user));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserSummaryResponse>> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(await BuildUserSummaryAsync(user));
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user)
    {
        var session = await _jwtTokenService.CreateSessionAsync(
            user,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.RequestAborted);

        return await BuildAuthResponseAsync(session);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(AuthSessionTokens session)
    {
        var roles = await _userManager.GetRolesAsync(session.User);
        SetRefreshTokenCookie(session.RefreshToken, session.RefreshTokenExpiresAtUtc);

        return new AuthResponse
        {
            AccessToken = session.AccessToken,
            ExpiresAtUtc = session.AccessTokenExpiresAtUtc,
            User = new UserSummaryResponse
            {
                Id = session.User.Id,
                UserName = session.User.UserName ?? string.Empty,
                Email = session.User.Email ?? string.Empty,
                Roles = roles.ToArray(),
                ProfileImageUrl = session.User.ProfileImageUrl,
                HasPassword = !string.IsNullOrWhiteSpace(session.User.PasswordHash)
            }
        };
    }

    private async Task<UserSummaryResponse> BuildUserSummaryAsync(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserSummaryResponse
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Roles = roles.ToArray(),
            ProfileImageUrl = user.ProfileImageUrl,
            HasPassword = !string.IsNullOrWhiteSpace(user.PasswordHash)
        };
    }

    private static UnauthorizedObjectResult InvalidCredentials()
    {
        return new UnauthorizedObjectResult(new { message = "Invalid email or password." });
    }

    private static UnauthorizedObjectResult InvalidSession()
    {
        return new UnauthorizedObjectResult(new { message = "The session is no longer valid." });
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(_jwtOptions.RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = _jwtOptions.RefreshTokenCookieSecure || Request.IsHttps,
            SameSite = ParseSameSiteMode(_jwtOptions.RefreshTokenCookieSameSite),
            Path = "/api/auth",
            Expires = expiresAtUtc,
            IsEssential = true
        });
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(_jwtOptions.RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = _jwtOptions.RefreshTokenCookieSecure || Request.IsHttps,
            SameSite = ParseSameSiteMode(_jwtOptions.RefreshTokenCookieSameSite),
            Path = "/api/auth",
            IsEssential = true
        });
    }

    private static SameSiteMode ParseSameSiteMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "none" => SameSiteMode.None,
            "strict" => SameSiteMode.Strict,
            _ => SameSiteMode.Lax
        };
    }

    private async Task<string> BuildUniqueUserNameAsync(string? displayName, string email)
    {
        var emailName = email.Split('@', 2)[0];
        var source = string.IsNullOrWhiteSpace(displayName) ? emailName : displayName.Trim();
        var candidate = SanitizeUserName(source);

        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = SanitizeUserName(emailName);
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = "GoogleUser";
        }

        candidate = candidate[..Math.Min(candidate.Length, 120)];
        var baseCandidate = candidate;
        var suffix = 2;

        while (await _userManager.FindByNameAsync(candidate) is not null)
        {
            var suffixText = $" {suffix}";
            candidate = baseCandidate[..Math.Min(baseCandidate.Length, 120 - suffixText.Length)] + suffixText;
            suffix++;
        }

        return candidate;
    }

    private static string SanitizeUserName(string value)
    {
        return new string(value
            .Where(character =>
                character is >= 'a' and <= 'z' ||
                character is >= 'A' and <= 'Z' ||
                character is >= '0' and <= '9' ||
                "-._@+ ".Contains(character))
            .ToArray())
            .Trim();
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }
    }
}
