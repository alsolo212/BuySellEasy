namespace Api.Contracts.Auth;

public class AuthResponse
{
    public required string AccessToken { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }

    public required UserSummaryResponse User { get; init; }
}
