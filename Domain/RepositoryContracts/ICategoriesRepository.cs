using Domain.Entities;

namespace Domain.RepositoryContracts
{
    public interface ICategoriesRepository : IGenericRepository<Category>
    {
        //List<Category> Categories { get; }
    }
}
