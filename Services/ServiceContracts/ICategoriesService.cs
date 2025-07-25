using Domain.Entities;

namespace Application.ServiceContracts
{
    public interface ICategoriesService
    {
        public List<Category> GetCategories();
        Category? GetCategoryById(Guid id);
    }
}
