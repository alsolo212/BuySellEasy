using Application.Interfaces;
using Domain.Entities;
using Domain.RepositoryContracts;

namespace Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;

        public UserService(IUserRepository repository)
        {
            _repository = repository;
        }

        public List<User> GetUsers()
        {
            return _repository.GetUsers();
        }

        public void RegisterUser(User user)
        {
            _repository.AddUser(user);
        }

        public User? Login(string email, string password)
        {
            return _repository.GetUserByEmailAndPassword(email, password);
        }
    }
}
