namespace Api.Contracts.Catalog;

public class CategoryListItemResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? ImageUrl { get; init; }
}
