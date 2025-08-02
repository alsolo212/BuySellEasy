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

        public async Task<List<Product>> GetProducts()
        {
            var products = await _repository.GetAllWithImagesAsync();
            return products?.ToList() ?? new List<Product>();
        }

        public async Task<Product?> GetProductById(Guid id)
        {
            var product = await _repository.GetProductWithImagesAsync(id);
            if (product != null && product.Images != null)
            {
                product.Images = product.Images
                    .Where(img => File.Exists(GetImagePhysicalPath(img.ImagePath)))
                    .OrderBy(img => img.SortOrder) // <- Сортировка здесь!
                    .ToList();
            }
            return product;
        }

        public async Task AddProduct(Product product)
        {
            if (product.Id == Guid.Empty)
                product.Id = Guid.NewGuid();

            product.CreatedAt ??= DateTime.Now;

            await _repository.AddAsync(product);
            await _repository.SaveAsync();
        }

        public async Task UpdateProduct(Product product)
        {
            var existing = await _repository.GetProductWithImagesAsync(product.Id);
            if (existing != null)
            {
                existing.Title = product.Title;
                existing.Description = product.Description;
                existing.ProductPrice = product.ProductPrice;
                existing.Location = product.Location;
                existing.ProductCondition = product.ProductCondition;

                _repository.UpdateObject(existing);
                await _repository.SaveAsync();
            }
        }

        public async Task DeleteProduct(Guid id)
        {
            var product = await _repository.GetProductWithImagesAsync(id);
            if (product != null)
            {
                if (product.Images != null)
                {
                    foreach (var image in product.Images)
                    {
                        var filePath = GetImagePhysicalPath(image.ImagePath);
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                }

                _repository.DeleteElement(product);
                await _repository.SaveAsync();
            }
        }

        private string GetImagePhysicalPath(string imagePath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/'));
        }
    }
}