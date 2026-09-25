using Domain.IdentityEntities;

namespace Api.Services;

public interface IJwtTokenService
{
    Task<(string Token, DateTime ExpiresAtUtc)> CreateTokenAsync(User user);
}
