using Domain.Entities;

namespace Domain.RepositoryContracts
{
    public interface ICategoriesRepository
    {
        public List<Category> GetCategories();
        Category? GetCategoryById(Guid id);
    }
}
