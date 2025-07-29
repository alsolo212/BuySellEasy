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
    }
}
