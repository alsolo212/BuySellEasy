using Domain.Enums;

namespace Api.Contracts.Orders;

public class ShipmentResponse
{
    public required Guid Id { get; init; }

    public ShipmentStatus Status { get; init; }

    public required string RecipientFirstName { get; init; }

    public required string RecipientLastName { get; init; }

    public required string RecipientPhone { get; init; }

    public required string DeliveryAddress { get; init; }

    public required string DeliveryCity { get; init; }

    public string SenderFirstName { get; init; } = string.Empty;

    public string SenderLastName { get; init; } = string.Empty;

    public string SenderPhone { get; init; } = string.Empty;

    public string PayoutCardMasked { get; init; } = string.Empty;

    public DateTime? ShippedAtUtc { get; init; }

    public DateTime? ArrivedAtUtc { get; init; }
}
