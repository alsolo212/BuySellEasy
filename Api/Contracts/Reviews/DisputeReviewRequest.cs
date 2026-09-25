using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Reviews;

public class DisputeReviewRequest
{
    [Required]
    [StringLength(2000)]
    public string Reason { get; init; } = string.Empty;
}
