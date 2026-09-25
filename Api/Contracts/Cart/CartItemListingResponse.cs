namespace Api.Contracts.Cart;

public class CartItemListingResponse
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public decimal Price { get; init; }

    public int AvailableQuantity { get; init; }

    public bool IsNegotiable { get; init; }

    public required string City { get; init; }

    public required string CategoryName { get; init; }

    public string? PrimaryImageUrl { get; init; }

    public required string SellerName { get; init; }
}
