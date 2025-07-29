using Domain.Entities;
using Domain.RepositoryContracts;
using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repository
{
    public class CategoriesRepository : GenericRepository<Category>, ICategoriesRepository
    {
        private readonly DbSet<Category> _dbSet;
        public CategoriesRepository(ProductDbContext dbContext) : base(dbContext)
        {
            _dbSet = dbContext.Set<Category>();
        }
    }
}
