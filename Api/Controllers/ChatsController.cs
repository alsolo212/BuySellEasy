using Api.Contracts.Chats;
using Api.Extensions;
using Api.Hubs;
using Api.Mappers;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chats")]
public class ChatsController : ControllerBase
{
    private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;
    private static readonly Dictionary<string, MessageType> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = MessageType.Image,
        [".jpeg"] = MessageType.Image,
        [".png"] = MessageType.Image,
        [".webp"] = MessageType.Image,
        [".gif"] = MessageType.Image,
        [".pdf"] = MessageType.File,
        [".doc"] = MessageType.File,
        [".docx"] = MessageType.File,
        [".txt"] = MessageType.File,
        [".zip"] = MessageType.File
    };

    private readonly ProductDbContext _dbContext;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ChatsController(
        ProductDbContext dbContext,
        IHubContext<ChatHub> hubContext,
        IWebHostEnvironment webHostEnvironment)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ChatSummaryResponse>>> GetMyChats(
        [FromQuery] Guid? userId = null,
        [FromQuery] bool supportOnly = false)
    {
        var currentUserId = User.GetRequiredUserId();
        var targetUserId = userId.HasValue && User.IsElevatedAdmin() ? userId.Value : currentUserId;
        var isViewingAnotherUsersChats = targetUserId != currentUserId;
        var chats = await _dbContext.Chats
            .AsNoTracking()
            .Include(chat => chat.Listing)
                .ThenInclude(listing => listing!.Images)
            .Include(chat => chat.Buyer)
            .Include(chat => chat.Seller)
            .Include(chat => chat.AssignedAdmin)
            .Include(chat => chat.Messages)
            .Where(chat => supportOnly
                ? chat.IsSupport
                : isViewingAnotherUsersChats
                    ? chat.BuyerId == targetUserId || chat.SellerId == targetUserId
                    : User.IsSuperAdmin()
                        ? chat.BuyerId == targetUserId || chat.SellerId == targetUserId || chat.IsSupport
                        : User.IsElevatedAdmin()
                            ? chat.BuyerId == targetUserId ||
                              chat.SellerId == targetUserId ||
                              (chat.IsSupport &&
                               (chat.AssignedAdminId == currentUserId ||
                                chat.Messages.Any(message => message.SenderId == currentUserId)))
                            : chat.BuyerId == targetUserId || chat.SellerId == targetUserId)
            .OrderByDescending(chat => chat.LastMessageAtUtc)
            .ToListAsync();

        return Ok(chats.Select(chat => MarketplaceMapper.MapChatSummary(chat, targetUserId)).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<ChatSummaryResponse>> StartChat([FromBody] StartChatRequest request)
    {
        var userId = User.GetRequiredUserId();
        var listing = await _dbContext.Listings
            .Include(item => item.Owner)
            .FirstOrDefaultAsync(item => item.Id == request.ListingId)
            ?? throw new KeyNotFoundException("Listing was not found.");

        if (listing.OwnerId == userId)
        {
            throw new InvalidOperationException("You cannot start a chat with yourself.");
        }

        var existingChat = await _dbContext.Chats
            .Include(chat => chat.Listing)
                .ThenInclude(listing => listing!.Images)
            .Include(chat => chat.Buyer)
            .Include(chat => chat.Seller)
            .Include(chat => chat.AssignedAdmin)
            .Include(chat => chat.Messages)
            .FirstOrDefaultAsync(chat =>
                chat.ListingId == request.ListingId &&
                chat.BuyerId == userId &&
                chat.SellerId == listing.OwnerId);

        if (existingChat is not null)
        {
            return Ok(MarketplaceMapper.MapChatSummary(existingChat, userId));
        }

        var chat = new Chat
        {
            ListingId = request.ListingId,
            BuyerId = userId,
            SellerId = listing.OwnerId,
            CreatedAtUtc = DateTime.UtcNow,
            LastMessageAtUtc = DateTime.UtcNow
        };

        _dbContext.Chats.Add(chat);
        await _dbContext.SaveChangesAsync();

        var created = await LoadChatAsync(chat.Id)
            ?? throw new KeyNotFoundException("Chat was not found.");

        await BroadcastChatSummariesAsync(created);
        return Ok(MarketplaceMapper.MapChatSummary(created, userId));
    }

    [HttpPost("support")]
    public async Task<ActionResult<StartSupportChatResponse>> StartSupportChat()
    {
        var userId = User.GetRequiredUserId();
        if (User.IsElevatedAdmin())
        {
            throw new InvalidOperationException("Admins already have access to support chats.");
        }

        var supportAccount = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == IdentitySeed.SuperAdminUserId)
            ?? throw new KeyNotFoundException("Support account was not found.");

        var existingChat = await _dbContext.Chats
            .Include(chat => chat.Listing)
                .ThenInclude(listing => listing!.Images)
            .Include(chat => chat.Buyer)
            .Include(chat => chat.Seller)
            .Include(chat => chat.AssignedAdmin)
            .Include(chat => chat.Messages)
            .FirstOrDefaultAsync(chat => chat.IsSupport && chat.BuyerId == userId);

        if (existingChat is not null)
        {
            return Ok(new StartSupportChatResponse
            {
                Chat = MarketplaceMapper.MapChatSummary(existingChat, userId)
            });
        }

        var chat = new Chat
        {
            BuyerId = userId,
            SellerId = supportAccount.Id,
            IsSupport = true,
            CreatedAtUtc = DateTime.UtcNow,
            LastMessageAtUtc = DateTime.UtcNow
        };

        _dbContext.Chats.Add(chat);
        await _dbContext.SaveChangesAsync();

        var created = await LoadChatAsync(chat.Id)
            ?? throw new KeyNotFoundException("Chat was not found.");

        await BroadcastChatSummariesAsync(created);

        return Ok(new StartSupportChatResponse
        {
            Chat = MarketplaceMapper.MapChatSummary(created, userId)
        });
    }

    [HttpPost("{id:guid}/claim")]
    public async Task<ActionResult<ChatSummaryResponse>> ClaimSupportChat(Guid id)
    {
        var userId = User.GetRequiredUserId();
        if (!User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("Only admins can claim support chats.");
        }

        var chat = await _dbContext.Chats
            .Include(item => item.Listing)
                .ThenInclude(listing => listing!.Images)
            .Include(item => item.Buyer)
            .Include(item => item.Seller)
            .Include(item => item.AssignedAdmin)
            .Include(item => item.Messages)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Chat was not found.");

        if (!chat.IsSupport)
        {
            throw new InvalidOperationException("Only support chats can be claimed.");
        }

        if (User.IsSuperAdmin())
        {
            return Ok(MarketplaceMapper.MapChatSummary(chat, userId));
        }

        if (chat.AssignedAdminId.HasValue && chat.AssignedAdminId != userId)
        {
            throw new InvalidOperationException("This support chat is already assigned to another admin.");
        }

        if (chat.AssignedAdminId != userId)
        {
            chat.AssignedAdminId = userId;
            await _dbContext.SaveChangesAsync();
            chat = await LoadChatAsync(chat.Id)
                ?? throw new KeyNotFoundException("Chat was not found.");
            await BroadcastChatSummariesAsync(chat);
        }

        return Ok(MarketplaceMapper.MapChatSummary(chat, userId));
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<IReadOnlyCollection<MessageResponse>>> GetMessages(Guid id)
    {
        var userId = User.GetRequiredUserId();
        var chat = await _dbContext.Chats
            .Include(item => item.Listing)
                .ThenInclude(listing => listing!.Images)
            .Include(item => item.Buyer)
            .Include(item => item.Seller)
            .Include(item => item.AssignedAdmin)
            .Include(item => item.Messages)
                .ThenInclude(message => message.Sender)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Chat was not found.");

        EnsureCanAccessChat(chat, userId);

        var unreadIncomingMessages = chat.Messages
            .Where(message => message.SenderId != userId && message.ReadAtUtc is null)
            .ToList();

        if (unreadIncomingMessages.Count > 0)
        {
            var readAtUtc = DateTime.UtcNow;
            foreach (var message in unreadIncomingMessages)
            {
                message.ReadAtUtc = readAtUtc;
            }

            await _dbContext.SaveChangesAsync();
            await BroadcastChatSummariesAsync(chat);
        }

        return Ok(chat.Messages
            .OrderBy(message => message.SentAtUtc)
            .Select(MarketplaceMapper.MapMessage)
            .ToArray());
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<MessageResponse>> SendMessage(Guid id, [FromBody] SendMessageRequest request)
    {
        var userId = User.GetRequiredUserId();
        var normalizedContent = request.Content?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedContent) && string.IsNullOrWhiteSpace(request.AttachmentUrl))
        {
            throw new ValidationException("Message must contain content or attachment.");
        }

        var chat = await _dbContext.Chats
            .Include(item => item.Listing)
                .ThenInclude(listing => listing!.Images)
            .Include(item => item.Buyer)
            .Include(item => item.Seller)
            .Include(item => item.AssignedAdmin)
            .Include(item => item.Messages)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Chat was not found.");

        EnsureCanAccessChat(chat, userId);
        EnsureSupportChatCanBeManaged(chat, userId);

        var message = new Message
        {
            ChatId = id,
            SenderId = userId,
            Content = normalizedContent,
            Type = request.Type,
            AttachmentUrl = request.AttachmentUrl?.Trim(),
            SentAtUtc = DateTime.UtcNow
        };

        _dbContext.Messages.Add(message);
        chat.LastMessageAtUtc = message.SentAtUtc;
        await _dbContext.SaveChangesAsync();

        var created = await _dbContext.Messages
            .AsNoTracking()
            .Include(item => item.Sender)
            .FirstOrDefaultAsync(item => item.Id == message.Id)
            ?? throw new KeyNotFoundException("Message was not found.");

        var response = MarketplaceMapper.MapMessage(created);
        await _hubContext.Clients.Group(ChatHub.BuildChatGroup(id)).SendAsync("MessageReceived", response);
        await BroadcastChatSummariesAsync(chat);

        return Ok(response);
    }

    [HttpPost("{id:guid}/attachments")]
    public async Task<ActionResult<ChatAttachmentUploadResponse>> UploadAttachment(Guid id, [FromForm] IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return ValidationProblem(CreateValidationProblem("file", "Attachment file is required."));
        }

        if (file.Length > MaxAttachmentSizeBytes)
        {
            return ValidationProblem(CreateValidationProblem("file", "Attachment must be smaller than 10 MB."));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedAttachmentExtensions.TryGetValue(extension, out var messageType))
        {
            return ValidationProblem(CreateValidationProblem("file", "This file type is not supported in chat."));
        }

        var userId = User.GetRequiredUserId();
        var chat = await _dbContext.Chats.FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Chat was not found.");

        EnsureCanAccessChat(chat, userId);
        EnsureSupportChatCanBeManaged(chat, userId);

        var uploadsDirectory = Path.Combine(ResolveWebRootPath(), "uploads", "chats");
        Directory.CreateDirectory(uploadsDirectory);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsDirectory, storedFileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new ChatAttachmentUploadResponse
        {
            FileName = file.FileName,
            AttachmentUrl = $"/uploads/chats/{storedFileName}",
            Type = messageType
        });
    }

    private void EnsureCanAccessChat(Chat chat, Guid currentUserId)
    {
        if (chat.BuyerId != currentUserId && chat.SellerId != currentUserId && !User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("You cannot access this chat.");
        }
    }

    private void EnsureSupportChatCanBeManaged(Chat chat, Guid currentUserId)
    {
        if (!chat.IsSupport || !User.IsElevatedAdmin() || User.IsSuperAdmin())
        {
            return;
        }

        if (chat.AssignedAdminId.HasValue && chat.AssignedAdminId != currentUserId)
        {
            throw new UnauthorizedAccessException("This support chat is already assigned to another admin.");
        }

        if (!chat.AssignedAdminId.HasValue)
        {
            chat.AssignedAdminId = currentUserId;
        }
    }

    private async Task<Chat?> LoadChatAsync(Guid chatId)
    {
        return await _dbContext.Chats
            .AsNoTracking()
            .Include(item => item.Listing)
                .ThenInclude(listing => listing!.Images)
            .Include(item => item.Buyer)
            .Include(item => item.Seller)
            .Include(item => item.AssignedAdmin)
            .Include(item => item.Messages)
            .FirstOrDefaultAsync(item => item.Id == chatId);
    }

    private async Task BroadcastChatSummariesAsync(Chat chat)
    {
        await _hubContext.Clients.Group(ChatHub.BuildUserGroup(chat.BuyerId))
            .SendAsync("ChatSummaryUpdated", MarketplaceMapper.MapChatSummary(chat, chat.BuyerId));
        await _hubContext.Clients.Group(ChatHub.BuildUserGroup(chat.SellerId))
            .SendAsync("ChatSummaryUpdated", MarketplaceMapper.MapChatSummary(chat, chat.SellerId));
        if (chat.AssignedAdminId.HasValue)
        {
            await _hubContext.Clients.Group(ChatHub.BuildUserGroup(chat.AssignedAdminId.Value))
                .SendAsync("ChatSummaryUpdated", MarketplaceMapper.MapChatSummary(chat, chat.AssignedAdminId.Value));
        }
        if (chat.IsSupport)
        {
            var adminRecipientId = chat.AssignedAdminId ?? chat.SellerId;
            await _hubContext.Clients.Group(ChatHub.BuildAdminsGroup())
                .SendAsync("ChatSummaryUpdated", MarketplaceMapper.MapChatSummary(chat, adminRecipientId));
        }
    }

    private static ValidationProblemDetails CreateValidationProblem(string key, string message)
    {
        return new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [key] = [message]
        });
    }

    private string ResolveWebRootPath()
    {
        return string.IsNullOrWhiteSpace(_webHostEnvironment.WebRootPath)
            ? Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot")
            : _webHostEnvironment.WebRootPath;
    }
}
