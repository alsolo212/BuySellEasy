using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Listings;

public class CreateListingRequest
{
    [Required]
    [StringLength(120)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(4000)]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 999999999)]
    public decimal Price { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; } = 1;

    public bool IsNegotiable { get; init; }

    [Required]
    [StringLength(120)]
    public string City { get; init; } = string.Empty;

    [Required]
    public Guid CategoryId { get; init; }

    [Required]
    public ListingCondition Condition { get; init; }

    public ListingStatus Status { get; init; } = ListingStatus.Published;

    public IReadOnlyCollection<string> ImageUrls { get; init; } = Array.Empty<string>();
}
