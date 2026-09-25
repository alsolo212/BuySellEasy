namespace Api.Contracts.Listings;

public class ListingImageResponse
{
    public required Guid Id { get; init; }

    public required string Url { get; init; }

    public int SortOrder { get; init; }
}
