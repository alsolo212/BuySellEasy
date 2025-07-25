using Domain.Entities;

namespace Domain.RepositoryContracts
{
    public interface IProductRepository
    {
        public List<Product> GetProducts();
        Product? GetProductById(Guid id);
        void AddProduct(Product product);
        void UpdateProduct(Product product);
        void DeleteProduct(Guid id);
    }
}
