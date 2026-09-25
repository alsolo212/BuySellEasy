using System.Security.Claims;
using Infrastructure.DbContextt;

namespace Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User identifier is missing.");
    }

    public static bool IsElevatedAdmin(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(IdentitySeed.Admin) || principal.IsInRole(IdentitySeed.SuperAdmin);
    }

    public static bool IsSuperAdmin(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(IdentitySeed.SuperAdmin);
    }
}
