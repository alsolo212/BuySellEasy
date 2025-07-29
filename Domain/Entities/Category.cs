using Domain.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Category : IHasId
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? Name { get; set; }
    }
}