using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Auth;

public class SetPasswordRequest
{
    [Required]
    [MinLength(6)]
    public string NewPassword { get; init; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; init; } = string.Empty;
}
