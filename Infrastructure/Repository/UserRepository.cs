using Domain.IdentityEntities;
using Domain.RepositoryContracts;
using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly ProductDbContext _context;

        public UserRepository(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public void Update(User user)
        {
            _context.Users.Update(user);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}