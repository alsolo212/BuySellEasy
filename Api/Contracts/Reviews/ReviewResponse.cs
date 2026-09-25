using Domain.Enums;

namespace Api.Contracts.Reviews;

public class ReviewResponse
{
    public required Guid Id { get; init; }

    public required Guid OrderId { get; init; }

    public required Guid ListingId { get; init; }

    public string? ListingTitle { get; init; }

    public required Guid AuthorId { get; init; }

    public required string AuthorName { get; init; }

    public required Guid TargetUserId { get; init; }

    public required string TargetUserName { get; init; }

    public int Rating { get; init; }

    public required string Comment { get; init; }

    public ReviewStatus Status { get; init; }

    public string? DisputeReason { get; init; }

    public string? OrderStatus { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
