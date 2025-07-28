using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace Infrastructure.DbContextt
{
    public class ProductDbContext : DbContext
    {
        public ProductDbContext(DbContextOptions options) : base(options) { }
        public DbSet<Product> Products {  get; set; }
        public DbSet<Category> Categories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>().ToTable("Products");
            modelBuilder.Entity<Category>().ToTable("Categories");

            //seed to Products
            string productsJson = System.IO.File.ReadAllText("products.json");
            List<Product> products = System.Text.Json.JsonSerializer.Deserialize<List<Product>>(productsJson);

            foreach (Product product in products)
                modelBuilder.Entity<Product>().HasData(product);

            //seed to Categories
            string categoriesJson = System.IO.File.ReadAllText("categories.json");
            List<Category> categories = System.Text.Json.JsonSerializer.Deserialize<List<Category>>(categoriesJson);

            foreach (Category category in categories)
                modelBuilder.Entity<Category>().HasData(category);
        }
    }
}
