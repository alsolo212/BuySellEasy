using Domain.IdentityEntities;

namespace Domain.RepositoryContracts
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        void Update(User user);
        Task SaveAsync();
    }
}