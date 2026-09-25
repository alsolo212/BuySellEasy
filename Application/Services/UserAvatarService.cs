using Application.ServiceContracts;
using Domain.IdentityEntities;
using Domain.RepositoryContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Application.Services
{
    public class UserAvatarService : IUserAvatarService
    {
        private readonly IUserRepository _userRepository;
        private readonly UserManager<User> _userManager;

        public UserAvatarService(IUserRepository userRepository, UserManager<User> userManager)
        {
            _userRepository = userRepository;
            _userManager = userManager;
        }

        public async Task<string?> UploadAvatarAsync(Guid userId, IFormFile avatarFile)
        {
            if (avatarFile == null || avatarFile.Length == 0)
                return null;

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return null;

            string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");

            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            // удаляем старый файл, если есть
            if (!string.IsNullOrEmpty(user.ProfileImageUrl))
            {
                var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfileImageUrl.TrimStart('/'));
                if (File.Exists(oldPath))
                    File.Delete(oldPath);
            }

            var fileName = Guid.NewGuid() + Path.GetExtension(avatarFile.FileName);
            var filePath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await avatarFile.CopyToAsync(stream);
            }

            user.ProfileImageUrl = $"/uploads/avatars/{fileName}";
            await _userManager.UpdateAsync(user);

            return user.ProfileImageUrl;
        }

        public async Task DeleteAvatarAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.ProfileImageUrl))
                return;

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfileImageUrl.TrimStart('/'));
            if (File.Exists(filePath))
                File.Delete(filePath);

            user.ProfileImageUrl = null;
            await _userManager.UpdateAsync(user);
        }
    }
}