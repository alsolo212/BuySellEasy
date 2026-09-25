using Domain.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class ListingImage : IHasId
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(2048)]
        public string Url { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public Guid ListingId { get; set; }

        public Listing Listing { get; set; } = null!;
    }
}
