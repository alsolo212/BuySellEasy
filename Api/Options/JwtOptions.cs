namespace Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    public int RefreshTokenLifetimeDays { get; set; } = 14;

    public int RefreshTokenAbsoluteLifetimeDays { get; set; } = 30;

    public string RefreshTokenCookieName { get; set; } = "bse_refresh_token";

    public bool RefreshTokenCookieSecure { get; set; }

    public string RefreshTokenCookieSameSite { get; set; } = "Lax";
}
