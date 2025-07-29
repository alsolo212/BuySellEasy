using Application.ServiceContracts;
using Domain.Entities;
using Domain.RepositoryContracts;

namespace Application.Services
{
    public class CategoriesService : ICategoriesService
    {
        private readonly ICategoriesRepository _repository;

        public CategoriesService(ICategoriesRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<Category>> GetCategories()
        {
            var categories = await _repository.GetAllValuesAsync();
            return categories?.ToList() ?? new List<Category>();
        }

        public async Task<Category?> GetCategoryById(Guid id)
        {
            return await _repository.GetValueByIdAsync(id);
        }
    }
}
