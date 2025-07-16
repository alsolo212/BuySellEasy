using Domain.Entities;
using Domain.RepositoryContracts;

public class UserList : IUserRepository
{
    public List<User> Users => new List<User>()
    {
        new User() { UserName = "Johns", Email = "qwe@gmail.com", Phone = "0991234567", Password = "123", ConfirmPassword = "123" },
        new User() { UserName = "Linda", Email = "rty@gmail.com", Phone = "0933215476", Password = "234", ConfirmPassword = "234" },
        new User() { UserName = "Susan", Email = "uio@gmail.com", Phone = null, Password = "345", ConfirmPassword = "345" }
    };
    public List<User> GetUsers()
    {
        return Users;
    }
}
