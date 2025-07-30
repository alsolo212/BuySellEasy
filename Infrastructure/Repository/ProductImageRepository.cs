using Domain.Entities;
using Domain.RepositoryContracts;
using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repository
{
    public class ProductImageRepository : GenericRepository<ProductImage>, IProductImageRepository
    {
        private readonly DbSet<ProductImage> _dbSet;
        public ProductImageRepository(ProductDbContext dbContext) : base(dbContext)
        {
            _dbSet = dbContext.Set<ProductImage>();
        }

        public async Task<List<ProductImage>> GetImagesByProductId(Guid productId)
        {
            return await _dbSet
                .Where(img => img.ProductId == productId)
                .ToListAsync();
        }
    }
}
