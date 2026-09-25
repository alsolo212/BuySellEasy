using Domain.Entities;
using Domain.IdentityEntities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DbContextt
{
    public class ProductDbContext : IdentityDbContext<User, Role, Guid>
    {
        public ProductDbContext(DbContextOptions options) : base(options) { }
        public DbSet<Product> Products {  get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Listing> Listings { get; set; }
        public DbSet<ListingImage> ListingImages { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Shipment> Shipments { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<ListingReport> ListingReports { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //Tables
            modelBuilder.Entity<Product>().ToTable("Products");
            modelBuilder.Entity<ProductImage>().ToTable("ProductImages");
            modelBuilder.Entity<Category>().ToTable("Categories");
            modelBuilder.Entity<Listing>().ToTable("Listings");
            modelBuilder.Entity<ListingImage>().ToTable("ListingImages");
            modelBuilder.Entity<Order>().ToTable("Orders");
            modelBuilder.Entity<Shipment>().ToTable("Shipments");
            modelBuilder.Entity<Chat>().ToTable("Chats");
            modelBuilder.Entity<Message>().ToTable("Messages");
            modelBuilder.Entity<Review>().ToTable("Reviews");
            modelBuilder.Entity<ListingReport>().ToTable("ListingReports");

            modelBuilder.Entity<Category>()
                .HasIndex(category => category.Name)
                .IsUnique();

            //Communication: One product - many images
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Images)
                .WithOne(i => i.Product)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // One category - many products
            modelBuilder.Entity<Category>()
                .HasMany(c => c.Products)
                .WithOne(p => p.Category)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // One user - many products
            modelBuilder.Entity<User>()
                .HasMany(u => u.Products)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One user - many cart items
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.User)
                .WithMany(u => u.CartItems)
                .HasForeignKey(ci => ci.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One product - many cart items
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany(p => p.CartItems)
                .HasForeignKey(ci => ci.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Listing)
                .WithMany(listing => listing.CartItems)
                .HasForeignKey(ci => ci.ListingId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            modelBuilder.Entity<CartItem>()
                .HasIndex(ci => new { ci.UserId, ci.ListingId })
                .IsUnique()
                .HasFilter("\"ListingId\" IS NOT NULL");

            // One category - many listings
            modelBuilder.Entity<Category>()
                .HasMany(category => category.Listings)
                .WithOne(listing => listing.Category)
                .HasForeignKey(listing => listing.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // One user - many listings
            modelBuilder.Entity<User>()
                .HasMany(user => user.Listings)
                .WithOne(listing => listing.Owner)
                .HasForeignKey(listing => listing.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Listing>()
                .Property(listing => listing.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Listing>()
                .HasMany(listing => listing.Images)
                .WithOne(image => image.Listing)
                .HasForeignKey(image => image.ListingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Listing>()
                .HasMany(listing => listing.Orders)
                .WithOne(order => order.Listing)
                .HasForeignKey(order => order.ListingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Listing>()
                .HasMany(listing => listing.Chats)
                .WithOne(chat => chat.Listing)
                .HasForeignKey(chat => chat.ListingId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Listing>()
                .HasMany(listing => listing.Reviews)
                .WithOne(review => review.Listing)
                .HasForeignKey(review => review.ListingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Listing>()
                .HasMany(listing => listing.Reports)
                .WithOne(report => report.Listing)
                .HasForeignKey(report => report.ListingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .Property(order => order.TotalAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<User>()
                .HasMany(user => user.PurchaseOrders)
                .WithOne(order => order.Buyer)
                .HasForeignKey(order => order.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasMany(user => user.SalesOrders)
                .WithOne(order => order.Seller)
                .HasForeignKey(order => order.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(order => order.Shipment)
                .WithOne(shipment => shipment.Order)
                .HasForeignKey<Shipment>(shipment => shipment.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasMany(order => order.Reviews)
                .WithOne(review => review.Order)
                .HasForeignKey(review => review.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasMany(user => user.BuyerChats)
                .WithOne(chat => chat.Buyer)
                .HasForeignKey(chat => chat.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasMany(user => user.SellerChats)
                .WithOne(chat => chat.Seller)
                .HasForeignKey(chat => chat.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Chat>()
                .HasOne(chat => chat.AssignedAdmin)
                .WithMany()
                .HasForeignKey(chat => chat.AssignedAdminId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Chat>()
                .HasIndex(chat => new { chat.ListingId, chat.BuyerId, chat.SellerId });

            modelBuilder.Entity<Chat>()
                .HasMany(chat => chat.Messages)
                .WithOne(message => message.Chat)
                .HasForeignKey(message => message.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(user => user.SentMessages)
                .WithOne(message => message.Sender)
                .HasForeignKey(message => message.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasMany(user => user.AuthoredReviews)
                .WithOne(review => review.Author)
                .HasForeignKey(review => review.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasMany(user => user.ReceivedReviews)
                .WithOne(review => review.TargetUser)
                .HasForeignKey(review => review.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ListingReport>()
                .HasOne(report => report.Reporter)
                .WithMany()
                .HasForeignKey(report => report.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ListingReport>()
                .HasOne(report => report.ResolvedBy)
                .WithMany()
                .HasForeignKey(report => report.ResolvedById)
                .OnDelete(DeleteBehavior.SetNull);

            //seed to Categories
            var categoriesJsonPath = FindCategoriesJsonPath();
            if (categoriesJsonPath is null)
            {
                return;
            }

            string categoriesJson = System.IO.File.ReadAllText(categoriesJsonPath);
            List<Category>? categories = System.Text.Json.JsonSerializer.Deserialize<List<Category>>(categoriesJson);

            if (categories != null)
            {
                modelBuilder.Entity<Category>().HasData(categories);
            }
        }

        private static string? FindCategoriesJsonPath()
        {
            var foldersToCheck = new List<string>
            {
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory()
            };

            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                foldersToCheck.Add(current.FullName);
                foldersToCheck.Add(Path.Combine(current.FullName, "UI"));
                foldersToCheck.Add(Path.Combine(current.FullName, "Api"));
                current = current.Parent;
            }

            foreach (var folder in foldersToCheck.Distinct())
            {
                var candidate = Path.Combine(folder, "categories.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
