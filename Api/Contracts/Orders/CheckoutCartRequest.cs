using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Orders;

public class CheckoutCartRequest
{
    public IReadOnlyCollection<Guid> CartItemIds { get; init; } = Array.Empty<Guid>();

    [Required]
    public PaymentMethod PaymentMethod { get; init; }

    [StringLength(32)]
    public string? CardNumber { get; init; }

    [StringLength(5)]
    public string? CardExpiry { get; init; }

    [StringLength(4)]
    public string? CardCvc { get; init; }

    [Required]
    [StringLength(120)]
    public string BuyerFirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string BuyerLastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string BuyerEmail { get; init; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(32)]
    public string BuyerPhone { get; init; } = string.Empty;

    [Required]
    [StringLength(512)]
    public string DeliveryAddress { get; init; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string DeliveryCity { get; init; } = string.Empty;
}
