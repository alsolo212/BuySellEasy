using Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Domain.IdentityEntities
{
    public class User : IdentityUser<Guid>
    {
        public string? ProfileImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLogin { get; set; }

        public bool IsVerified { get; set; } = true;

        public int ActiveListingsCount { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}