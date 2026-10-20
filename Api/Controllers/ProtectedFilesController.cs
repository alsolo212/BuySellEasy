using Api.Extensions;
using Api.Services;
using Domain.Entities;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>
/// Serves private uploads without exposing the whole web root through static files.
/// Listing images intentionally remain public and are served by the dedicated
/// static-file provider in Program.cs.
/// </summary>
[ApiController]
[Authorize]
[Route("uploads")]
public sealed class ProtectedFilesController : ControllerBase
{
    private readonly ProductDbContext _dbContext;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ProtectedFilesController(ProductDbContext dbContext, IWebHostEnvironment webHostEnvironment)
    {
        _dbContext = dbContext;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet("avatars/{fileName}")]
    public IActionResult GetAvatar(string fileName)
    {
        if (!TryGetSafeFileName(fileName, out var safeFileName) ||
            !TryGetAvatarOwnerId(safeFileName, out var ownerId))
        {
            return NotFound();
        }

        var currentUserId = User.GetRequiredUserId();
        if (currentUserId != ownerId && !User.IsElevatedAdmin())
        {
            return NotFound();
        }

        var path = Path.Combine(ResolveWebRootPath(), "uploads", "avatars", safeFileName);
        return ServeFile(path, safeFileName);
    }

    [HttpGet("chats/{chatId:guid}/{fileName}")]
    public async Task<IActionResult> GetChatAttachment(Guid chatId, string fileName)
    {
        if (!TryGetSafeFileName(fileName, out var safeFileName))
        {
            return NotFound();
        }

        var chat = await _dbContext.Chats
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == chatId);

        if (chat is null || !CanAccessChat(chat))
        {
            return NotFound();
        }

        var path = Path.Combine(
            ResolveWebRootPath(),
            "uploads",
            "chats",
            chatId.ToString("N"),
            safeFileName);
        return ServeFile(path, safeFileName);
    }

    // Keep already persisted legacy attachment URLs working, but still protect
    // them by resolving the owning message and checking its chat membership.
    [HttpGet("chats/{fileName}")]
    public async Task<IActionResult> GetLegacyChatAttachment(string fileName)
    {
        if (!TryGetSafeFileName(fileName, out var safeFileName))
        {
            return NotFound();
        }

        var attachmentUrl = $"/uploads/chats/{safeFileName}";
        var message = await _dbContext.Messages
            .AsNoTracking()
            .Include(item => item.Chat)
            .FirstOrDefaultAsync(item => item.AttachmentUrl == attachmentUrl);

        if (message?.Chat is null || !CanAccessChat(message.Chat))
        {
            return NotFound();
        }

        var path = Path.Combine(ResolveWebRootPath(), "uploads", "chats", safeFileName);
        return ServeFile(path, safeFileName);
    }

    private bool CanAccessChat(Chat chat)
    {
        var currentUserId = User.GetRequiredUserId();
        return chat.BuyerId == currentUserId ||
               chat.SellerId == currentUserId ||
               User.IsElevatedAdmin();
    }

    private IActionResult ServeFile(string path, string fileName)
    {
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(
            path,
            FileUploadSecurityService.GetContentType(fileName),
            enableRangeProcessing: true);
    }

    private string ResolveWebRootPath()
    {
        return string.IsNullOrWhiteSpace(_webHostEnvironment.WebRootPath)
            ? Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot")
            : _webHostEnvironment.WebRootPath;
    }

    private static bool TryGetSafeFileName(string fileName, out string safeFileName)
    {
        safeFileName = fileName?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(safeFileName) &&
               safeFileName == Path.GetFileName(safeFileName) &&
               safeFileName.IndexOfAny(['/', '\\', '\0']) < 0 &&
               safeFileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static bool TryGetAvatarOwnerId(string fileName, out Guid ownerId)
    {
        ownerId = Guid.Empty;
        var separatorIndex = fileName.IndexOf('-');
        return separatorIndex > 0 &&
               Guid.TryParseExact(fileName[..separatorIndex], "N", out ownerId);
    }
}
