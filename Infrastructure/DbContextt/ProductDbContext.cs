using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace Infrastructure.DbContextt
{
    public class ProductDbContext : DbContext
    {
        public ProductDbContext(DbContextOptions options) : base(options) { }
        public DbSet<Product> Products {  get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Category> Categories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //Tables
            modelBuilder.Entity<Product>().ToTable("Products");
            modelBuilder.Entity<ProductImage>().ToTable("ProductImages");
            modelBuilder.Entity<Category>().ToTable("Categories");

            //Communication: One product - many images
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Images)
                .WithOne(i => i.Product)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            //seed to Products
            string productsJson = System.IO.File.ReadAllText("products.json");
            List<Product>? products = System.Text.Json.JsonSerializer.Deserialize<List<Product>>(productsJson);

            if (products != null)
            {
                modelBuilder.Entity<Product>().HasData(products);
            }

            //seed to ProductImages
            string productImagesJson = System.IO.File.ReadAllText("productImages.json");
            List<ProductImage>? productImages = System.Text.Json.JsonSerializer.Deserialize<List<ProductImage>>(productImagesJson);

            if (productImages != null)
            {
                modelBuilder.Entity<ProductImage>().HasData(productImages);
            }

            //seed to Categories
            string categoriesJson = System.IO.File.ReadAllText("categories.json");
            List<Category>? categories = System.Text.Json.JsonSerializer.Deserialize<List<Category>>(categoriesJson);

            if (categories != null)
            {
                modelBuilder.Entity<Category>().HasData(categories);
            }
        }
    }
}
