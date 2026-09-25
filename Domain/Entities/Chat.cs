using Domain.Abstractions;
using Domain.IdentityEntities;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Chat : IHasId
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? ListingId { get; set; }

        public Guid BuyerId { get; set; }

        public Guid SellerId { get; set; }

        public bool IsSupport { get; set; }

        public Guid? AssignedAdminId { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime LastMessageAtUtc { get; set; } = DateTime.UtcNow;

        public Listing? Listing { get; set; }

        public User Buyer { get; set; } = null!;

        public User Seller { get; set; } = null!;

        public User? AssignedAdmin { get; set; }

        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
