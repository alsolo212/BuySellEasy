using Domain.Entities;

namespace Application.ServiceContracts
{
    public interface IProductService
    {
        public Task<List<Product>> GetProducts();

        public Task<Product?> GetProductById(Guid id);

        public Task AddProduct(Product product);

        public Task UpdateProduct(Product product);

        public Task DeleteProduct(Guid id);
    }
}