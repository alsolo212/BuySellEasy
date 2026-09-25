namespace Api.Contracts.Auth;

public class UserSummaryResponse
{
    public required Guid Id { get; init; }

    public required string UserName { get; init; }

    public required string Email { get; init; }

    public required IReadOnlyCollection<string> Roles { get; init; }

    public string? ProfileImageUrl { get; init; }
}
