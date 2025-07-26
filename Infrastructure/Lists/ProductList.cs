using Domain.Entities;
using Domain.RepositoryContracts;

namespace Infrastructure.Lists
{
    public class ProductList : IProductRepository
    {
        public static List<Product> _products = new List<Product>()
        {
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "T-shirt",
                Description = "Black T-shirt size L is suitable for men with a height of 180+ centimeters",
                ProductPrice = 123,
                Location = "Kyiv",
                CreatedAt = DateTime.Now,
                ProductCondition = "Used"
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Boots",
                Description = "Leather winter boots",
                ProductPrice = 234,
                Location = "Lviv",
                CreatedAt = DateTime.Now,
                ProductCondition = "Open Box"
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Watch",
                Description = "Stylish men's wristwatch",
                ProductPrice = 345,
                Location = "Odesa",
                CreatedAt = DateTime.Now,
                ProductCondition = "New"
            }
        };

        public List<Product> Products => _products;
    }
}
