using Domain.Abstractions;
using Domain.IdentityEntities;

namespace Domain.Entities
{
    public class CartItem : IHasId
    {
        public Guid Id { get; set; }

        // FK на пользователя
        public Guid UserId { get; set; }
        public User? User { get; set; }

        // FK на продукт
        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;
    }
}
