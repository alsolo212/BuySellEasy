using Microsoft.AspNetCore.Http;

namespace Application.ServiceContracts
{
    public interface IUserAvatarService
    {
        Task<string?> UploadAvatarAsync(Guid userId, IFormFile avatarFile);
        Task DeleteAvatarAsync(Guid userId);
    }
}