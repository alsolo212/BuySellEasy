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

        public List<Category> GetCategories() => _repository.GetCategories();
        public Category? GetCategoryById(Guid id) => _repository.GetCategoryById(id);
    }
}
