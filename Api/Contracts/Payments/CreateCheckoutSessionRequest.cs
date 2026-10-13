using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Payments;

public class CreateCheckoutSessionRequest
{
    public Guid? ListingId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; } = 1;

    public IReadOnlyCollection<Guid> CartItemIds { get; init; } = Array.Empty<Guid>();

    public bool SaveCard { get; init; }

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
