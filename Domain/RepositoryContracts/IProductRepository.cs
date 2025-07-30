using Domain.Entities;

namespace Domain.RepositoryContracts
{
    public interface IProductRepository : IGenericRepository<Product>
    {
        Task<Product?> GetProductWithImagesAsync(Guid id);
        Task<IEnumerable<Product>> GetAllWithImagesAsync();
    }
}
