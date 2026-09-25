using Domain.Enums;

namespace Api.Contracts.Reviews;

public class UpdateReviewModerationRequest
{
    public ReviewStatus Status { get; init; }
}
