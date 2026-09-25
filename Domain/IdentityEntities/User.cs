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

        public bool IsBlocked { get; set; }

        public int ActiveListingsCount { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public ICollection<Listing> Listings { get; set; } = new List<Listing>();
        public ICollection<Order> PurchaseOrders { get; set; } = new List<Order>();
        public ICollection<Order> SalesOrders { get; set; } = new List<Order>();
        public ICollection<Chat> BuyerChats { get; set; } = new List<Chat>();
        public ICollection<Chat> SellerChats { get; set; } = new List<Chat>();
        public ICollection<Message> SentMessages { get; set; } = new List<Message>();
        public ICollection<Review> AuthoredReviews { get; set; } = new List<Review>();
        public ICollection<Review> ReceivedReviews { get; set; } = new List<Review>();
    }
}
