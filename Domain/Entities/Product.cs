using Domain.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Product : IHasId
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [StringLength(35)]
        public string? Title { get; set; }
        public string? Description { get; set; }
        public double? ProductPrice { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public string? Location { get; set; }
        public string? ProductCondition { get; set; }

        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }
}