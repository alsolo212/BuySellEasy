using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Auth;

public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; init; } = string.Empty;
}
