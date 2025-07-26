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

        public List<Category> GetCategories()
        {
            return _repository.Categories.ToList();
        }

        public Category? GetCategoryById(Guid id)
        {
            return _repository.Categories.FirstOrDefault(p => p.Id == id);
        }
    }
}
