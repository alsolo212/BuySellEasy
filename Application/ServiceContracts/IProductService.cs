using Domain.Entities;

namespace Application.ServiceContracts
{
    public interface IProductService
    {
        public List<Product> GetProducts();
        Product? GetProductById(Guid id);
        void AddProduct(Product product);
        void UpdateProduct(Product product);
        void DeleteProduct(Guid id);
    }
}
