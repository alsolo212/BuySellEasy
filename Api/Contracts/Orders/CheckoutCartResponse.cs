namespace Api.Contracts.Orders;

public class CheckoutCartResponse
{
    public required IReadOnlyCollection<OrderDetailsResponse> Orders { get; init; }

    public decimal TotalAmount { get; init; }
}
