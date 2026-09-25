using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Users;

public class AdminUpdateUserRequest
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

    [StringLength(256)]
    public string? ProfileImageUrl { get; init; }

    [StringLength(32)]
    public string? Role { get; init; }
}
