using Domain.Entities;
using Domain.RepositoryContracts;

public class ProductList : IProductRepository
{
    private static List<Product> _products = new List<Product>()
    {
        new Product()
        {
            Id = Guid.NewGuid(),
            Title = "T-shirt",
            Description = "Black T-shirt size L is suitable for men with a height of 180+ centimeters",
            ProductPrice = 123,
            Location = "Kyiv",
            CreatedAt = DateTime.Now,
            ProductCondition = "Used"
        },
        new Product()
        {
            Id = Guid.NewGuid(),
            Title = "Boots",
            Description = "Leather winter boots",
            ProductPrice = 234,
            Location = "Lviv",
            CreatedAt = DateTime.Now,
            ProductCondition = "Open Box"
        },
        new Product()
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

    public List<Product> GetProducts() => _products;

    public Product? GetProductById(Guid id) =>
        _products.FirstOrDefault(p => p.Id == id);

    public void AddProduct(Product product)
    {
        if (product.Id == Guid.Empty)
            product.Id = Guid.NewGuid();

        product.CreatedAt ??= DateTime.Now;

        _products.Add(product);
    }

    public void UpdateProduct(Product product)
    {
        var existing = _products.FirstOrDefault(p => p.Id == product.Id);
        if (existing != null)
        {
            existing.Title = product.Title;
            existing.Description = product.Description;
            existing.ProductPrice = product.ProductPrice;
            existing.Location = product.Location;
            existing.ProductCondition = product.ProductCondition;
        }
    }

    public void DeleteProduct(Guid id)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product != null)
            _products.Remove(product);
    }
}
