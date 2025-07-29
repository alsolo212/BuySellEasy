using Domain.Entities;

namespace Application.ServiceContracts
{
    public interface ICategoriesService
    {
        public Task<List<Category>> GetCategories();
        public Task<Category?> GetCategoryById(Guid id);
    }
}
