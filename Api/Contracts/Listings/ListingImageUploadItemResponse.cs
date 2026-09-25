namespace Api.Contracts.Listings;

public class ListingImageUploadItemResponse
{
    public required string Url { get; init; }

    public required string FileName { get; init; }
}
