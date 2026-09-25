using Domain.Enums;

namespace Api.Contracts.Orders;

public class OrderSummaryResponse
{
    public required Guid Id { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    public string? ListingPrimaryImageUrl { get; init; }

    public required Guid BuyerId { get; init; }

    public required Guid SellerId { get; init; }

    public string CounterpartyName { get; init; } = string.Empty;

    public OrderStatus Status { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    public int Quantity { get; init; }

    public string? PaymentCardMasked { get; init; }

    public decimal TotalAmount { get; init; }

    public required string DeliveryCity { get; init; }

    public required string DeliveryAddress { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public ShipmentStatus? ShipmentStatus { get; init; }

    public bool HasReviewFromCurrentUser { get; init; }
}
