using Domain.Abstractions;
using Domain.IdentityEntities;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class CartItem : IHasId
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User? User { get; set; }

        // Legacy MVC cart relation. Keep nullable for safe migration.
        public Guid? ProductId { get; set; }
        public Product? Product { get; set; }

        public Guid? ListingId { get; set; }
        public Listing? Listing { get; set; }

        public bool IsSelected { get; set; } = true;

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
