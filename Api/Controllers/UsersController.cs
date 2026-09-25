using Api.Contracts.Users;
using Api.Extensions;
using Api.Mappers;
using Domain.IdentityEntities;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private const long AvatarMaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedAvatarExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly ProductDbContext _dbContext;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly UserManager<User> _userManager;

    public UsersController(
        ProductDbContext dbContext,
        UserManager<User> userManager,
        IWebHostEnvironment webHostEnvironment)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _webHostEnvironment = webHostEnvironment;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> GetMe()
    {
        var userId = User.GetRequiredUserId();
        var user = await LoadProfileAsync(userId);
        return Ok(MarketplaceMapper.MapUserProfile(user));
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<UserProfileResponse>> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var userId = User.GetRequiredUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString()) ?? throw new KeyNotFoundException("User was not found.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
        if (!passwordValid)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["Current password is incorrect."]
            }));
        }

        var previousProfileImageUrl = user.ProfileImageUrl;
        user.UserName = request.UserName.Trim();
        user.Email = request.Email.Trim();
        user.PhoneNumber = request.Phone?.Trim();
        user.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl)
            ? null
            : request.ProfileImageUrl.Trim();

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        if (!string.Equals(previousProfileImageUrl, user.ProfileImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            DeleteCurrentAvatarIfManaged(previousProfileImageUrl, ResolveWebRootPath());
        }

        var updatedUser = await LoadProfileAsync(userId);
        return Ok(MarketplaceMapper.MapUserProfile(updatedUser));
    }

    [Authorize]
    [HttpPost("me/avatar")]
    public async Task<ActionResult<AvatarUploadResponse>> UploadAvatar([FromForm] IFormFile? file)
    {
        var userId = User.GetRequiredUserId();
        return await UploadAvatarInternalAsync(userId, file);
    }

    [HttpGet("{id:guid}/profile")]
    public async Task<ActionResult<UserProfileResponse>> GetPublicProfile(Guid id)
    {
        var user = await LoadProfileAsync(id);
        return Ok(MarketplaceMapper.MapUserProfile(user));
    }

    [Authorize(Roles = $"{IdentitySeed.Admin},{IdentitySeed.SuperAdmin}")]
    [HttpGet("admin")]
    public async Task<ActionResult<IReadOnlyCollection<AdminUserListItemResponse>>> GetUsers([FromQuery] string? searchTerm)
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.UserName)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalized = searchTerm.Trim().ToLower();
            users = users
                .Where(user =>
                    (user.UserName ?? string.Empty).ToLower().Contains(normalized) ||
                    (user.Email ?? string.Empty).ToLower().Contains(normalized))
                .ToList();
        }

        var result = new List<AdminUserListItemResponse>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new AdminUserListItemResponse
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                ProfileImageUrl = user.ProfileImageUrl,
                Phone = user.PhoneNumber,
                IsVerified = user.IsVerified,
                IsBlocked = user.IsBlocked,
                ActiveListingsCount = user.ActiveListingsCount,
                Roles = roles.ToArray()
            });
        }

        return Ok(result);
    }

    [Authorize(Roles = $"{IdentitySeed.Admin},{IdentitySeed.SuperAdmin}")]
    [HttpGet("admin/{id:guid}")]
    public async Task<ActionResult<AdminUserDetailsResponse>> GetUserForAdmin(Guid id)
    {
        var user = await LoadProfileAsync(id);
        var roles = await _userManager.GetRolesAsync(user);
        var profile = MarketplaceMapper.MapUserProfile(user);

        return Ok(new AdminUserDetailsResponse
        {
            Id = profile.Id,
            UserName = profile.UserName,
            Email = profile.Email,
            Phone = profile.Phone,
            ProfileImageUrl = profile.ProfileImageUrl,
            CreatedAtUtc = profile.CreatedAtUtc,
            IsVerified = profile.IsVerified,
            IsBlocked = profile.IsBlocked,
            ActiveListingsCount = profile.ActiveListingsCount,
            AverageRating = profile.AverageRating,
            ReviewsCount = profile.ReviewsCount,
            Roles = roles.ToArray()
        });
    }

    [Authorize(Roles = $"{IdentitySeed.Admin},{IdentitySeed.SuperAdmin}")]
    [HttpPut("admin/{id:guid}")]
    public async Task<ActionResult<AdminUserDetailsResponse>> UpdateUserForAdmin(Guid id, [FromBody] AdminUpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString()) ?? throw new KeyNotFoundException("User was not found.");

        var targetRoles = await _userManager.GetRolesAsync(user);
        if (targetRoles.Contains(IdentitySeed.SuperAdmin) && !User.IsSuperAdmin())
        {
            throw new UnauthorizedAccessException("Only super admin can manage another super admin.");
        }

        user.UserName = request.UserName.Trim();
        user.Email = request.Email.Trim();
        user.PhoneNumber = request.Phone?.Trim();
        user.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl)
            ? null
            : request.ProfileImageUrl.Trim();

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!User.IsSuperAdmin())
            {
                throw new UnauthorizedAccessException("Only super admin can change roles.");
            }

            await UpdateRolesAsync(user, request.Role.Trim());
        }

        var updated = await LoadProfileAsync(user.Id);
        var updatedRoles = await _userManager.GetRolesAsync(updated);
        var profile = MarketplaceMapper.MapUserProfile(updated);

        return Ok(new AdminUserDetailsResponse
        {
            Id = profile.Id,
            UserName = profile.UserName,
            Email = profile.Email,
            Phone = profile.Phone,
            ProfileImageUrl = profile.ProfileImageUrl,
            CreatedAtUtc = profile.CreatedAtUtc,
            IsVerified = profile.IsVerified,
            IsBlocked = profile.IsBlocked,
            ActiveListingsCount = profile.ActiveListingsCount,
            AverageRating = profile.AverageRating,
            ReviewsCount = profile.ReviewsCount,
            Roles = updatedRoles.ToArray()
        });
    }

    [Authorize(Roles = $"{IdentitySeed.Admin},{IdentitySeed.SuperAdmin}")]
    [HttpPost("admin/{id:guid}/avatar")]
    public async Task<ActionResult<AvatarUploadResponse>> UploadAvatarForAdmin(Guid id, [FromForm] IFormFile? file)
    {
        var targetUser = await _userManager.FindByIdAsync(id.ToString()) ?? throw new KeyNotFoundException("User was not found.");
        var targetRoles = await _userManager.GetRolesAsync(targetUser);
        if (targetRoles.Contains(IdentitySeed.SuperAdmin) && !User.IsSuperAdmin())
        {
            throw new UnauthorizedAccessException("Only super admin can manage another super admin.");
        }

        return await UploadAvatarInternalAsync(targetUser.Id, file);
    }

    [Authorize(Roles = $"{IdentitySeed.Admin},{IdentitySeed.SuperAdmin}")]
    [HttpPatch("admin/{id:guid}/block")]
    public async Task<ActionResult<AdminUserDetailsResponse>> ToggleBlockForAdmin(Guid id, [FromBody] AdminToggleBlockRequest request)
    {
        var currentUserId = User.GetRequiredUserId();
        if (currentUserId == id)
        {
            throw new InvalidOperationException("You cannot block your own account.");
        }

        var user = await _userManager.FindByIdAsync(id.ToString()) ?? throw new KeyNotFoundException("User was not found.");
        var targetRoles = await _userManager.GetRolesAsync(user);

        if (targetRoles.Contains(IdentitySeed.SuperAdmin))
        {
            throw new UnauthorizedAccessException("Super admin cannot be blocked.");
        }

        if (targetRoles.Contains(IdentitySeed.Admin) && !User.IsSuperAdmin())
        {
            throw new UnauthorizedAccessException("Only super admin can block another admin.");
        }

        user.IsBlocked = request.IsBlocked;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        var updated = await LoadProfileAsync(id);
        var updatedRoles = await _userManager.GetRolesAsync(updated);
        var profile = MarketplaceMapper.MapUserProfile(updated);

        return Ok(new AdminUserDetailsResponse
        {
            Id = profile.Id,
            UserName = profile.UserName,
            Email = profile.Email,
            Phone = profile.Phone,
            ProfileImageUrl = profile.ProfileImageUrl,
            CreatedAtUtc = profile.CreatedAtUtc,
            IsVerified = profile.IsVerified,
            IsBlocked = profile.IsBlocked,
            ActiveListingsCount = profile.ActiveListingsCount,
            AverageRating = profile.AverageRating,
            ReviewsCount = profile.ReviewsCount,
            Roles = updatedRoles.ToArray()
        });
    }

    private async Task<User> LoadProfileAsync(Guid userId)
    {
        return await _dbContext.Users
            .Include(user => user.ReceivedReviews)
            .FirstOrDefaultAsync(user => user.Id == userId)
            ?? throw new KeyNotFoundException("User was not found.");
    }

    private async Task UpdateRolesAsync(User user, string requestedRole)
    {
        var normalizedRole = requestedRole.Trim();
        if (normalizedRole is not IdentitySeed.User and not IdentitySeed.Admin)
        {
            throw new ValidationException("Role can be only User or Admin.");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var desiredRoles = normalizedRole == IdentitySeed.Admin
            ? new[] { IdentitySeed.User, IdentitySeed.Admin }
            : new[] { IdentitySeed.User };

        var rolesToRemove = currentRoles
            .Where(role => role != IdentitySeed.SuperAdmin && !desiredRoles.Contains(role))
            .ToArray();
        var rolesToAdd = desiredRoles
            .Where(role => !currentRoles.Contains(role))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                var errors = string.Join("; ", removeResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException(errors);
            }
        }

        if (rolesToAdd.Length > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                var errors = string.Join("; ", addResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException(errors);
            }
        }
    }

    private static ValidationProblemDetails CreateValidationProblem(string key, string message)
    {
        return new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [key] = [message]
        });
    }

    private static void DeleteCurrentAvatarIfManaged(string? currentAvatarUrl, string webRootPath)
    {
        if (string.IsNullOrWhiteSpace(currentAvatarUrl) || !currentAvatarUrl.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var currentFileName = Path.GetFileName(currentAvatarUrl);
        if (string.IsNullOrWhiteSpace(currentFileName))
        {
            return;
        }

        var currentFilePath = Path.Combine(webRootPath, "uploads", "avatars", currentFileName);
        if (System.IO.File.Exists(currentFilePath))
        {
            System.IO.File.Delete(currentFilePath);
        }
    }

    private string ResolveWebRootPath()
    {
        return string.IsNullOrWhiteSpace(_webHostEnvironment.WebRootPath)
            ? Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot")
            : _webHostEnvironment.WebRootPath;
    }

    private async Task<ActionResult<AvatarUploadResponse>> UploadAvatarInternalAsync(Guid targetUserId, IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return ValidationProblem(CreateValidationProblem("file", "Avatar file is required."));
        }

        if (file.Length > AvatarMaxFileSizeBytes)
        {
            return ValidationProblem(CreateValidationProblem("file", "Avatar file must be smaller than 5 MB."));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedAvatarExtensions.Contains(extension))
        {
            return ValidationProblem(CreateValidationProblem("file", "Only JPG, PNG, and WEBP images are supported."));
        }

        var userExists = await _userManager.Users.AnyAsync(user => user.Id == targetUserId);
        if (!userExists)
        {
            throw new KeyNotFoundException("User was not found.");
        }

        var webRootPath = ResolveWebRootPath();
        var avatarsDirectory = Path.Combine(webRootPath, "uploads", "avatars");
        Directory.CreateDirectory(avatarsDirectory);

        var fileName = $"{targetUserId:N}-{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(avatarsDirectory, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new AvatarUploadResponse
        {
            ProfileImageUrl = $"/uploads/avatars/{fileName}"
        });
    }
}
