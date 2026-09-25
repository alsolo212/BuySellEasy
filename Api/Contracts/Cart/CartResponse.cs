namespace Api.Contracts.Cart;

public class CartResponse
{
    public required IReadOnlyCollection<CartItemResponse> Items { get; init; }

    public decimal SelectedTotalAmount { get; init; }
}
