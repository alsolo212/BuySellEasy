using System.ComponentModel.DataAnnotations;

namespace Domain.IdentityEntities;

public class RefreshToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    [Required]
    [StringLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public Guid FamilyId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime AbsoluteExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    [StringLength(64)]
    public string? ReplacedByTokenHash { get; set; }

    [StringLength(64)]
    public string? CreatedByIp { get; set; }

    [StringLength(64)]
    public string? RevokedByIp { get; set; }

    public User User { get; set; } = null!;
}
