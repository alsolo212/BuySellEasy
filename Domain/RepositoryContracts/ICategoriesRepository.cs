using Domain.Entities;

namespace Domain.RepositoryContracts
{
    public interface ICategoriesRepository
    {
        List<Category> Categories { get; }
    }
}
