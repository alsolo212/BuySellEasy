using Domain.Abstractions;
using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Shipment : IHasId
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrderId { get; set; }

        public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;

        [Required]
        [StringLength(120)]
        public string RecipientFirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string RecipientLastName { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(32)]
        public string RecipientPhone { get; set; } = string.Empty;

        [Required]
        [StringLength(512)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string DeliveryCity { get; set; } = string.Empty;

        [StringLength(120)]
        public string SenderFirstName { get; set; } = string.Empty;

        [StringLength(120)]
        public string SenderLastName { get; set; } = string.Empty;

        [Phone]
        [StringLength(32)]
        public string SenderPhone { get; set; } = string.Empty;

        [StringLength(128)]
        public string PayoutCardMasked { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ShippedAtUtc { get; set; }

        public DateTime? ArrivedAtUtc { get; set; }

        public Order Order { get; set; } = null!;
    }
}
