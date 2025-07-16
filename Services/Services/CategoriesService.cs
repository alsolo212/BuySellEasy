using ServiceContracts.Interfaces;
using Domain.RepositoryContracts;

namespace Services.Services
{
    public class CategoriesService : ICategoriesService
    {
        private readonly ICategoriesRepository _repository;

        public CategoriesService(ICategoriesRepository repository)
        {
            _repository = repository;
        }

        public List<string> GetCategories()
        {
            return _repository.GetCategories();
        }
    }
}
