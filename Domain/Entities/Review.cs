using Domain.Abstractions;
using Domain.Enums;
using Domain.IdentityEntities;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Review : IHasId
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrderId { get; set; }

        public Guid ListingId { get; set; }

        public Guid AuthorId { get; set; }

        public Guid TargetUserId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        [StringLength(2000)]
        public string Comment { get; set; } = string.Empty;

        public ReviewStatus Status { get; set; } = ReviewStatus.Published;

        [StringLength(2000)]
        public string? DisputeReason { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public Order Order { get; set; } = null!;

        public Listing Listing { get; set; } = null!;

        public User Author { get; set; } = null!;

        public User TargetUser { get; set; } = null!;
    }
}
