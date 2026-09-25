using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Listings;

public class CreateListingReportRequest
{
    [Required]
    [StringLength(1000)]
    public string Reason { get; init; } = string.Empty;
}
