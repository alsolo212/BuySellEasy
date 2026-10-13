using Domain.Abstractions;
using Domain.Enums;
using Domain.IdentityEntities;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public class PaymentSession : IHasId
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BuyerId { get; set; }

    [Required]
    [StringLength(128)]
    public string StripeCheckoutSessionId { get; set; } = string.Empty;

    [StringLength(128)]
    public string? StripePaymentIntentId { get; set; }

    public PaymentSessionStatus Status { get; set; } = PaymentSessionStatus.Pending;

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal TotalAmount { get; set; }

    [Required]
    public string DraftJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAtUtc { get; set; }

    public User Buyer { get; set; } = null!;
}
