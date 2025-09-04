using Domain.Entities;
using Domain.RepositoryContracts;
using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repository
{
    public class CartItemRepository : GenericRepository<CartItem>, ICartItemRepository
    {
        private readonly ProductDbContext _context;

        public CartItemRepository(ProductDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IEnumerable<CartItem>> GetUserCartItemsAsync(Guid userId)
        {
            return await _context.CartItems
                .Include(ci => ci.Product)
                .ThenInclude(p => p.Images)
                .Where(ci => ci.UserId == userId)
                .ToListAsync();
        }

        public async Task<CartItem?> GetCartItemByUserAndProductAsync(Guid userId, Guid productId)
        {
            return await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == productId);
        }
    }
}