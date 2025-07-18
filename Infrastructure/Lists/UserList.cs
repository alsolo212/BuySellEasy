using Domain.Entities;
using Domain.RepositoryContracts;

public class UserList : IUserRepository
{
    private static List<User> _users = new List<User>()
    {
        new User() { UserName = "Johns", Email = "qwe@gmail.com", Phone = "0991234567", Password = "123" },
        new User() { UserName = "Linda", Email = "rty@gmail.com", Phone = "0933215476", Password = "234" },
        new User() { UserName = "Susan", Email = "uio@gmail.com", Phone = null, Password = "345" }
    };

    public List<User> GetUsers() => _users;

    public void AddUser(User user)
    {
        _users.Add(user);
    }

    public User? GetUserByEmailAndPassword(string email, string password)
    {
        return _users.FirstOrDefault(u => u.Email == email && u.Password == password);
    }
}
