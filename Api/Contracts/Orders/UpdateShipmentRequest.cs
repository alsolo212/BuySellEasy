using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Orders;

public class UpdateShipmentRequest
{
    [Required]
    public ShipmentStatus Status { get; init; }

    [StringLength(120)]
    public string SenderFirstName { get; init; } = string.Empty;

    [StringLength(120)]
    public string SenderLastName { get; init; } = string.Empty;

    [Phone]
    [StringLength(32)]
    public string SenderPhone { get; init; } = string.Empty;

    [StringLength(128)]
    public string PayoutCardMasked { get; init; } = string.Empty;
}
