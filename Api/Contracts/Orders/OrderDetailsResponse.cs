using Api.Contracts.Listings;
using Api.Contracts.Users;
using Domain.Enums;

namespace Api.Contracts.Orders;

public class OrderDetailsResponse
{
    public required Guid Id { get; init; }

    public OrderStatus Status { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    public int Quantity { get; init; }

    public string? PaymentCardMasked { get; init; }

    public decimal TotalAmount { get; init; }

    public required string BuyerFirstName { get; init; }

    public required string BuyerLastName { get; init; }

    public required string BuyerEmail { get; init; }

    public required string BuyerPhone { get; init; }

    public required string DeliveryAddress { get; init; }

    public required string DeliveryCity { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public required ListingSummaryResponse Listing { get; init; }

    public required UserProfileResponse Buyer { get; init; }

    public required UserProfileResponse Seller { get; init; }

    public ShipmentResponse? Shipment { get; init; }
}
