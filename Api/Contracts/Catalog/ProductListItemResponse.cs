namespace Api.Contracts.Catalog;

public class ProductListItemResponse
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public string Description { get; init; } = string.Empty;

    public double? Price { get; init; }

    public DateTime? CreatedAt { get; init; }

    public string Location { get; init; } = string.Empty;

    public string Condition { get; init; } = string.Empty;

    public required CategoryListItemResponse Category { get; init; }

    public string? ImageUrl { get; init; }

    public required SellerSummaryResponse Seller { get; init; }
}
