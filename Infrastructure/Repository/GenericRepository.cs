using Domain.Abstractions;
using Domain.RepositoryContracts;
using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Infrastructure.Repository
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class, IHasId
    {
        private readonly ProductDbContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(ProductDbContext dbContext)
        {
            _context = dbContext;
            _dbSet = _context.Set<T>();
        }

        public async Task<Boolean> IsObjectIdExists(Guid id)
        {
            return await _dbSet.AnyAsync(obj => obj.Id == id);
        }

        public async Task<IEnumerable<T>?> GetAllValuesAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task<T?> GetValueByIdAsync(Guid id)
        {
            return await _dbSet.FirstOrDefaultAsync(obj => obj.Id == id);
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public void UpdateObject(T entity)
        {
            _dbSet.Update(entity);
        }

        public void DeleteElement(T entity)
        {
            _dbSet.Remove(entity);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
