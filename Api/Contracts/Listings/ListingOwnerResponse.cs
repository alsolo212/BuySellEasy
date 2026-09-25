namespace Api.Contracts.Listings;

public class ListingOwnerResponse
{
    public required Guid Id { get; init; }

    public required string UserName { get; init; }

    public string? ProfileImageUrl { get; init; }

    public bool IsVerified { get; init; }
}
