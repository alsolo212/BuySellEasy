using Domain.Entities;
using Domain.RepositoryContracts;

namespace Application.Services
{
    public class CartService : ICartService
    {
        private readonly ICartItemRepository _cartItemRepository;
        private readonly IGenericRepository<Product> _productRepository;

        public CartService(ICartItemRepository cartItemRepository, IGenericRepository<Product> productRepository)
        {
            _cartItemRepository = cartItemRepository;
            _productRepository = productRepository;
        }

        public async Task<bool> AddToCartAsync(Guid userId, Guid productId)
        {
            var product = await _productRepository.GetValueByIdAsync(productId);

            if (product == null || product.UserId == userId)
                return false; // нельзя добавить свой товар или несуществующий

            var existing = await _cartItemRepository.GetCartItemByUserAndProductAsync(userId, productId);
            if (existing != null)
                return false; // уже в корзине

            var cartItem = new CartItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProductId = productId
            };

            await _cartItemRepository.AddAsync(cartItem);
            await _cartItemRepository.SaveAsync();
            return true;
        }

        public async Task<bool> RemoveFromCartAsync(Guid cartItemId, Guid userId)
        {
            var cartItem = await _cartItemRepository.GetValueByIdAsync(cartItemId);

            if (cartItem == null || cartItem.UserId != userId)
                return false; // нельзя удалить чужой cart item

            _cartItemRepository.DeleteElement(cartItem);
            await _cartItemRepository.SaveAsync();
            return true;
        }

        public async Task<IEnumerable<CartItem>> GetUserCartAsync(Guid userId)
        {
            return await _cartItemRepository.GetUserCartItemsAsync(userId);
        }

        public async Task<IEnumerable<CartItem>> GetCartItemsByUserIdAsync(Guid userId)
        {
            return await _cartItemRepository.GetUserCartItemsAsync(userId);
        }

        public async Task DeleteCartItemAsync(Guid cartItemId)
        {
            var cartItem = await _cartItemRepository.GetValueByIdAsync(cartItemId);
            if (cartItem != null)
            {
                _cartItemRepository.DeleteElement(cartItem);
                await _cartItemRepository.SaveAsync();
            }
        }
    }
}
