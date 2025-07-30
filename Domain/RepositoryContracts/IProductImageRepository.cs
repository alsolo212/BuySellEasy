using Domain.Entities;

namespace Domain.RepositoryContracts
{
    public interface IProductImageRepository : IGenericRepository<ProductImage>
    {
        Task<List<ProductImage>> GetImagesByProductId(Guid productId);
    }
}
