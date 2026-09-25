using Domain.Abstractions;
using Domain.Enums;
using Domain.IdentityEntities;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Listing : IHasId
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(4000)]
        public string Description { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "999999999")]
        public decimal Price { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;

        public bool IsNegotiable { get; set; }

        [Required]
        [StringLength(120)]
        public string City { get; set; } = string.Empty;

        public ListingCondition Condition { get; set; } = ListingCondition.Used;

        public ListingStatus Status { get; set; } = ListingStatus.ReadyToShip;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? PublishedAtUtc { get; set; } = DateTime.UtcNow;

        public Guid CategoryId { get; set; }

        public Guid OwnerId { get; set; }

        public Category Category { get; set; } = null!;

        public User Owner { get; set; } = null!;

        public ICollection<ListingImage> Images { get; set; } = new List<ListingImage>();

        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

        public ICollection<Order> Orders { get; set; } = new List<Order>();

        public ICollection<Chat> Chats { get; set; } = new List<Chat>();

        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        public ICollection<ListingReport> Reports { get; set; } = new List<ListingReport>();
    }
}
