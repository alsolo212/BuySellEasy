using Domain.Entities;

namespace Application.Interfaces
{
    public interface IUserService
    {
        public List<User> GetUsers();
        void RegisterUser(User user);
        User? Login(string email, string password);
    }
}
