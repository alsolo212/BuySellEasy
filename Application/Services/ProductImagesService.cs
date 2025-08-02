using Application.ServiceContracts;
using Domain.Entities;
using Domain.RepositoryContracts;
using static System.Net.Mime.MediaTypeNames;

namespace Application.Services
{
    public class ProductImagesService : IProductImageService
    {
        private readonly IProductImageRepository _repository;

        public ProductImagesService(IProductImageRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ProductImage>> GetProductImages()
        {
            var productImages = await _repository.GetAllValuesAsync();
            return productImages?.ToList() ?? new List<ProductImage>();
        }

        public async Task<ProductImage?> GetProductImageById(Guid id)
        {
            return await _repository.GetValueByIdAsync(id);
        }

        public async Task AddProductImage(ProductImage productImage)
        {
            if (productImage.Id == Guid.Empty)
                productImage.Id = Guid.NewGuid();

            // Получаем текущий максимум SortOrder и добавляем +1
            int maxSortOrder = await _repository.GetMaxSortOrderAsync(productImage.ProductId);
            productImage.SortOrder = maxSortOrder + 1;

            await _repository.AddAsync(productImage);
            await _repository.SaveAsync();
        }

        public async Task UpdateProductImage(ProductImage productImage)
        {
            var existing = await _repository.GetValueByIdAsync(productImage.Id);
            if (existing != null)
            {
                existing.ImagePath = productImage.ImagePath;
                existing.ProductId = productImage.ProductId;

                _repository.UpdateObject(existing);
                await _repository.SaveAsync();
            }
        }

        public async Task DeleteProductImage(Guid id)
        {
            var productImage = await _repository.GetValueByIdAsync(id);
            if (productImage != null)
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", productImage.ImagePath.TrimStart('/'));
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Удаляем запись из БД
                _repository.DeleteElement(productImage);
                await _repository.SaveAsync();
            }
        }
    }
}
