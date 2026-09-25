namespace Api.Contracts.Catalog;

public class ProductImageResponse
{
    public required Guid Id { get; init; }

    public required string Url { get; init; }

    public int SortOrder { get; init; }
}
