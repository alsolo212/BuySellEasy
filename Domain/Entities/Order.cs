using Domain.Abstractions;
using Domain.Enums;
using Domain.IdentityEntities;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Order : IHasId
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ListingId { get; set; }

        public Guid BuyerId { get; set; }

        public Guid SellerId { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Card;

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;

        [Range(typeof(decimal), "0.01", "999999999")]
        public decimal TotalAmount { get; set; }

        [StringLength(32)]
        public string? PaymentCardMasked { get; set; }

        [Required]
        [StringLength(120)]
        public string ListingTitleSnapshot { get; set; } = string.Empty;

        [StringLength(2048)]
        public string? ListingPrimaryImageUrlSnapshot { get; set; }

        [Required]
        [StringLength(120)]
        public string BuyerFirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string BuyerLastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string BuyerEmail { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(32)]
        public string BuyerPhone { get; set; } = string.Empty;

        [Required]
        [StringLength(512)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string DeliveryCity { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public Listing Listing { get; set; } = null!;

        public User Buyer { get; set; } = null!;

        public User Seller { get; set; } = null!;

        public Shipment? Shipment { get; set; }

        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
