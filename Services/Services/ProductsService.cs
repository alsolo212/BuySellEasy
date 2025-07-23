using Application.Interfaces;
using Domain.Entities;
using Domain.RepositoryContracts;

namespace Application.Services
{
    public class ProductsService : IProductService
    {
        private readonly IProductRepository _repository;

        public ProductsService(IProductRepository repository)
        {
            _repository = repository;
        }

        public List<Product> GetProducts() => _repository.GetProducts();
        public Product? GetProductById(Guid id) => _repository.GetProductById(id);
        public void AddProduct(Product product) => _repository.AddProduct(product);
        public void UpdateProduct(Product product) => _repository.UpdateProduct(product);
        public void DeleteProduct(Guid id) => _repository.DeleteProduct(id);
    }
}
