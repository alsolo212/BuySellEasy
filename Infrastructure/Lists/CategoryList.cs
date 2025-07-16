using Domain.RepositoryContracts;

namespace Infrastructure.Lists
{
    public class CategoryList : ICategoriesRepository
    {
        public List<string> AllCategories => new List<string>()
        {
            "All categories",
            "Clothes",
            "Cars",
            "Job",
            "Sport",
            "Pets"
        };

        public List<string> GetCategories() {
            return AllCategories;
        }
    }

}
