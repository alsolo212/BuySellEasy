namespace Api.Contracts.Users;

public class PublicUserProfileResponse
{
    public required Guid Id { get; init; }

    public required string UserName { get; init; }

    public string? ProfileImageUrl { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public bool IsVerified { get; init; }

    public int ActiveListingsCount { get; init; }

    public double AverageRating { get; init; }

    public int ReviewsCount { get; init; }
}
