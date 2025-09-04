using Domain.Entities;

namespace Application.Services
{
    public interface ICartService
    {
        Task<bool> AddToCartAsync(Guid userId, Guid productId);
        Task<bool> RemoveFromCartAsync(Guid cartItemId, Guid userId);
        Task<IEnumerable<CartItem>> GetUserCartAsync(Guid userId);
        Task<IEnumerable<CartItem>> GetCartItemsByUserIdAsync(Guid userId);
        Task DeleteCartItemAsync(Guid cartItemId);
    }
}