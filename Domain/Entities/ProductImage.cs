using Domain.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities
{
    public class ProductImage : IHasId
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ImagePath { get; set; } = string.Empty;

        [ForeignKey(nameof(Product))]
        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;
    }
}