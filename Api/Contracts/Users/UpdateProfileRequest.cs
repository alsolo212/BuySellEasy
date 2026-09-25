using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Users;

public class UpdateProfileRequest
{
    [Required]
    [StringLength(120)]
    public string UserName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Phone]
    [StringLength(32)]
    public string? Phone { get; init; }

    [StringLength(2048)]
    public string? ProfileImageUrl { get; init; }

    [Required]
    [StringLength(128)]
    public string CurrentPassword { get; init; } = string.Empty;
}
