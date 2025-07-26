using Domain.Entities;
using Domain.RepositoryContracts;

namespace Infrastructure.Lists
{
    public class CategoryList : ICategoriesRepository
    {
        public static List<Category> _category = new List<Category>()
        {
            new Category()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Clothes"
            },
            new Category()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Cars"
            },
            new Category()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Job"
            },
            new Category()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Sport"
            },
            new Category()
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "Pets"
            }
        };

        public List<Category> Categories => _category;
    }
}