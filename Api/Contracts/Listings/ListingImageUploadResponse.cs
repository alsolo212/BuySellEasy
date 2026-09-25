namespace Api.Contracts.Listings;

public class ListingImageUploadResponse
{
    public required IReadOnlyCollection<ListingImageUploadItemResponse> Images { get; init; }
}
