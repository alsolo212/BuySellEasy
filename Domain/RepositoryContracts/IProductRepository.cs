using Domain.Entities;

namespace Domain.RepositoryContracts
{
    public interface IProductRepository
    {
        List<Product> Products { get; }
    }
}
