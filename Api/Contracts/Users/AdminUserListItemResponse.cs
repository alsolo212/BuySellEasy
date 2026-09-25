namespace Api.Contracts.Users;

public class AdminUserListItemResponse
{
    public required Guid Id { get; init; }

    public required string UserName { get; init; }

    public required string Email { get; init; }

    public string? ProfileImageUrl { get; init; }

    public string? Phone { get; init; }

    public bool IsVerified { get; init; }

    public bool IsBlocked { get; init; }

    public int ActiveListingsCount { get; init; }

    public required IReadOnlyCollection<string> Roles { get; init; }
}
