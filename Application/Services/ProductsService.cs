using Application.ServiceContracts;
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

        public List<Product> GetProducts()
        {
            return _repository.Products.ToList(); // Возвращаем копию, чтобы не было внешнего изменения
        }

        public Product? GetProductById(Guid id)
        {
            return _repository.Products.FirstOrDefault(p => p.Id == id);
        }

        public void AddProduct(Product product)
        {
            if (product.Id == Guid.Empty)
                product.Id = Guid.NewGuid();

            product.CreatedAt ??= DateTime.Now;

            _repository.Products.Add(product);
        }

        public void UpdateProduct(Product product)
        {
            var existing = _repository.Products.FirstOrDefault(p => p.Id == product.Id);
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
            var product = _repository.Products.FirstOrDefault(p => p.Id == id);
            if (product != null)
                _repository.Products.Remove(product);
        }
    }
}
