using Domain.Abstractions;
using Domain.Enums;
using Domain.IdentityEntities;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public class ListingReport : IHasId
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ListingId { get; set; }

    public Guid ReporterId { get; set; }

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public ListingReportStatus Status { get; set; } = ListingReportStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }

    public Guid? ResolvedById { get; set; }

    public Listing Listing { get; set; } = null!;

    public User Reporter { get; set; } = null!;

    public User? ResolvedBy { get; set; }
}
