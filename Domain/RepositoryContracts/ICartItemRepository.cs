using Domain.Entities;

namespace Domain.RepositoryContracts
{
    public interface ICartItemRepository : IGenericRepository<CartItem>
    {
        Task<IEnumerable<CartItem>> GetUserCartItemsAsync(Guid userId);
        Task<CartItem?> GetCartItemByUserAndProductAsync(Guid userId, Guid productId);
    }
}