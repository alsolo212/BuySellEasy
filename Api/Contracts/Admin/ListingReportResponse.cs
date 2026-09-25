using Domain.Enums;

namespace Api.Contracts.Admin;

public class ListingReportResponse
{
    public required Guid Id { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    public string? ListingPrimaryImageUrl { get; init; }

    public required Guid OwnerId { get; init; }

    public required string OwnerName { get; init; }

    public required Guid ReporterId { get; init; }

    public required string ReporterName { get; init; }

    public required string Reason { get; init; }

    public ListingReportStatus Status { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? ResolvedAtUtc { get; init; }
}
