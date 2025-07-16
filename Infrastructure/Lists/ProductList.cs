using Domain.Entities;
using Domain.RepositoryContracts;

public class ProductList : IProductRepository
{
    public List<Product> Products => new List<Product>()
    {
        new Product() { ProductId = 1, ProductName = "T-shirt", ProductPrice = 123 },
        new Product() { ProductId = 2, ProductName = "Boots", ProductPrice = 234 },
        new Product() { ProductId = 3, ProductName = "Watch", ProductPrice = 345 }
    };

    public List<Product> GetProducts()
    {
        return Products;
    }
}
