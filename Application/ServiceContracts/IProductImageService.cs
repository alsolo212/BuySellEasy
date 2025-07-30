using Domain.Entities;

namespace Application.ServiceContracts
{
    public interface IProductImageService
    {
        public Task<List<ProductImage>> GetProductImages();

        public Task<ProductImage?> GetProductImageById(Guid id);

        public Task AddProductImage(ProductImage productImage);

        public Task UpdateProductImage(ProductImage productImage);

        public Task DeleteProductImage(Guid id);
    }
}
