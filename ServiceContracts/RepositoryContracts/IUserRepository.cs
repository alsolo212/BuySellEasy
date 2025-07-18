using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.RepositoryContracts
{
    public interface IUserRepository
    {
        public List<User> GetUsers();
        void AddUser(User user);
        User? GetUserByEmailAndPassword(string email, string password);
    }
}
