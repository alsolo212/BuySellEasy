using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Reviews;

public class CreateReviewRequest
{
    [Required]
    public Guid OrderId { get; init; }

    public Guid? TargetUserId { get; init; }

    [Range(1, 5)]
    public int Rating { get; init; }

    [Required]
    [StringLength(2000)]
    public string Comment { get; init; } = string.Empty;
}
