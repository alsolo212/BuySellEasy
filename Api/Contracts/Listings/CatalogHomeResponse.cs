using Api.Contracts.Catalog;

namespace Api.Contracts.Listings;

public class CatalogHomeResponse
{
    public required IReadOnlyCollection<CategoryListItemResponse> Categories { get; init; }

    public required IReadOnlyCollection<ListingSummaryResponse> FeaturedListings { get; init; }
}
