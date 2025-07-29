using Domain.Entities;

namespace Domain.RepositoryContracts
{
    public interface IProductRepository : IGenericRepository<Product>
    {
        //List<Product> Products { get; }
    }
}
