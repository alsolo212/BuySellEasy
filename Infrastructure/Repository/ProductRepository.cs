using Domain.Entities;
using Domain.RepositoryContracts;
using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repository
{
    public class ProductRepository : GenericRepository<Product>, IProductRepository
    {
        private readonly DbSet<Product> _dbSet;
        public ProductRepository(ProductDbContext dbContext) : base(dbContext)
        {
            _dbSet = dbContext.Set<Product>();
        }

        public async Task<Product?> GetProductWithImagesAsync(Guid id)
        {
            return await _dbSet
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<Product>> GetAllWithImagesAsync()
        {
            return await _dbSet
                .Include(p => p.Category)
                .Include(p => p.Images)
                .ToListAsync();
        }
    }
}