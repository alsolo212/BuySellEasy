namespace Api.Contracts.Cart;

public class CartItemResponse
{
    public required Guid Id { get; init; }

    public bool IsSelected { get; init; }

    public int Quantity { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public required CartItemListingResponse Listing { get; init; }
}
