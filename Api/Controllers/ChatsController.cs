using Api.Contracts.Chats;
using Api.Extensions;
using Api.Hubs;
using Api.Mappers;
using Api.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chats")]
public class ChatsController : ControllerBase
{
    private static readonly TimeSpan AdminInactivityTimeout = TimeSpan.FromMinutes(20);

    private readonly ProductDbContext _dbContext;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly SupportBotService _supportBot;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly FileUploadSecurityService _fileUploads;

    public ChatsController(
        ProductDbContext dbContext,
        IHubContext<ChatHub> hubContext,
        SupportBotService supportBot,
        IWebHostEnvironment webHostEnvironment,
        FileUploadSecurityService fileUploads)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _supportBot = supportBot;
        _webHostEnvironment = webHostEnvironment;
        _fileUploads = fileUploads;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ChatSummaryResponse>>> GetMyChats(
        [FromQuery] Guid? userId = null,
        [FromQuery] bool supportOnly = false)
    {
        var currentUserId = User.GetRequiredUserId();
        var targetUserId = userId.HasValue && User.IsElevatedAdmin() ? userId.Value : currentUserId;
        var isViewingAnotherUsersChats = targetUserId != currentUserId;
        IQueryable<Chat> chatsQuery = _dbContext.Chats
            .AsNoTracking()
            .Include(chat => chat.Listing)
                .ThenInclude(listing => listing!.Images)
            .Include(chat => chat.Buyer)
            .Include(chat => chat.Seller)
            .Include(chat => chat.AssignedAdmin)
            .Include(chat => chat.Messages);

        if (supportOnly)
        {
            // The supportOnly flag is a view filter, not an authorization bypass.
            // Regular users may only see their own support chat; support staff may
            // see the support inbox so that they can claim and handle chats.
            chatsQuery = User.IsElevatedAdmin()
                ? chatsQuery.Where(chat => chat.IsSupport)
                : chatsQuery.Where(chat => chat.IsSupport && chat.BuyerId == currentUserId);
        }
        else if (isViewingAnotherUsersChats)
        {
            chatsQuery = chatsQuery.Where(chat =>
                chat.BuyerId == targetUserId || chat.SellerId == targetUserId);
        }
        else if (User.IsSuperAdmin())
        {
            chatsQuery = chatsQuery.Where(chat =>
                chat.BuyerId == targetUserId || chat.SellerId == targetUserId || chat.IsSupport);
        }
        else if (User.IsElevatedAdmin())
        {
            chatsQuery = chatsQuery.Where(chat =>
                chat.BuyerId == targetUserId ||
                chat.SellerId == targetUserId ||
                (chat.IsSupport &&
                 (chat.AssignedAdminId == currentUserId ||
                  chat.Messages.Any(message => message.SenderId == currentUserId))));
        }
        else
        {
            chatsQuery = chatsQuery.Where(chat =>
                chat.BuyerId == targetUserId || chat.SellerId == targetUserId);
        }

        var chats = await chatsQuery
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
            SupportStatus = SupportChatStatus.BotActive,
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

    [HttpPost("{id:guid}/request-admin")]
    public async Task<ActionResult<ChatSummaryResponse>> RequestAdmin(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var chat = await _dbContext.Chats
            .Include(item => item.Listing)
                .ThenInclude(listing => listing!.Images)
            .Include(item => item.Buyer)
            .Include(item => item.Seller)
            .Include(item => item.AssignedAdmin)
            .Include(item => item.Messages)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Chat was not found.");

        if (!CanAccessChat(chat, userId))
        {
            return Forbid();
        }

        if (!chat.IsSupport)
        {
            throw new InvalidOperationException("Only support chats can be escalated.");
        }

        if (chat.SupportStatus == SupportChatStatus.BotActive)
        {
            chat.SupportStatus = SupportChatStatus.NeedsAdmin;
            await AddSupportBotMessageAsync(chat, SupportBotService.HandoffMessage, cancellationToken);
            await BroadcastChatSummariesAsync(chat);
        }

        return Ok(MarketplaceMapper.MapChatSummary(chat, userId));
    }

    [HttpPost("{id:guid}/claim")]
    public async Task<ActionResult<ChatSummaryResponse>> ClaimSupportChat(Guid id)
    {
        var userId = User.GetRequiredUserId();
        if (!User.IsElevatedAdmin())
        {
            return Forbid();
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

        if (!CanAccessChat(chat, userId))
        {
            return Forbid();
        }

        if (!chat.IsSupport)
        {
            throw new InvalidOperationException("Only support chats can be claimed.");
        }

        if (chat.AssignedAdminId.HasValue && chat.AssignedAdminId != userId)
        {
            throw new InvalidOperationException("This support chat is already assigned to another admin.");
        }

        if (chat.AssignedAdminId != userId || chat.SupportStatus != SupportChatStatus.AdminActive)
        {
            chat.AssignedAdminId = userId;
            chat.SupportStatus = SupportChatStatus.AdminActive;
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

        if (!CanAccessChat(chat, userId))
        {
            return Forbid();
        }

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
    public async Task<ActionResult<MessageResponse>> SendMessage(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var normalizedContent = request.Content?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedContent) && string.IsNullOrWhiteSpace(request.AttachmentUrl))
        {
            throw new ValidationException("Message must contain content or attachment.");
        }

        var attachmentUrl = request.AttachmentUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(attachmentUrl) && !IsAttachmentForChat(attachmentUrl, id))
        {
            throw new ValidationException("The attachment does not belong to this chat.");
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

        if (!CanAccessChat(chat, userId))
        {
            return Forbid();
        }

        if (!TryEnsureSupportChatCanBeManaged(chat, userId))
        {
            return Forbid();
        }

        var message = new Message
        {
            ChatId = id,
            SenderId = userId,
            Content = normalizedContent,
            Type = request.Type,
            AttachmentUrl = attachmentUrl,
            SentAtUtc = DateTime.UtcNow
        };

        _dbContext.Messages.Add(message);
        chat.LastMessageAtUtc = message.SentAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.Messages
            .AsNoTracking()
            .Include(item => item.Sender)
            .FirstOrDefaultAsync(item => item.Id == message.Id)
            ?? throw new KeyNotFoundException("Message was not found.");

        var response = MarketplaceMapper.MapMessage(created);
        await _hubContext.Clients.Group(ChatHub.BuildChatGroup(id)).SendAsync("MessageReceived", response);
        await ProcessSupportMessageAsync(chat, userId, message, cancellationToken);
        await BroadcastChatSummariesAsync(chat);

        return Ok(response);
    }

    [HttpPost("{id:guid}/attachments")]
    [EnableRateLimiting(ApiServiceCollectionExtensions.FileUploadRateLimitPolicyName)]
    [RequestSizeLimit(FileUploadSecurityService.MaxChatRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileUploadSecurityService.MaxChatRequestBytes)]
    public async Task<ActionResult<ChatAttachmentUploadResponse>> UploadAttachment(
        Guid id,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ValidationProblem(CreateValidationProblem("file", "Attachment file is required."));
        }

        var userId = User.GetRequiredUserId();
        var chat = await _dbContext.Chats.FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Chat was not found.");

        if (!CanAccessChat(chat, userId))
        {
            return Forbid();
        }

        if (!TryEnsureSupportChatCanBeManaged(chat, userId))
        {
            return Forbid();
        }

        var validatedFile = await _fileUploads.ValidateAsync(
            file,
            FileUploadKind.ChatAttachment,
            cancellationToken);

        var uploadsDirectory = Path.Combine(ResolveWebRootPath(), "uploads", "chats", id.ToString("N"));
        Directory.CreateDirectory(uploadsDirectory);

        var storedFileName = $"{Guid.NewGuid():N}{validatedFile.Extension}";
        var filePath = Path.Combine(uploadsDirectory, storedFileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return Ok(new ChatAttachmentUploadResponse
        {
            FileName = file.FileName,
            AttachmentUrl = $"/uploads/chats/{id:N}/{storedFileName}",
            Type = validatedFile.MessageType ?? MessageType.File
        });
    }

    private bool IsAttachmentForChat(string attachmentUrl, Guid chatId)
    {
        var prefix = $"/uploads/chats/{chatId:N}/";
        if (!attachmentUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = attachmentUrl[prefix.Length..];
        return !string.IsNullOrWhiteSpace(fileName) &&
            fileName == Path.GetFileName(fileName) &&
            fileName.IndexOfAny(['/', '\\', '\0']) < 0 &&
            System.IO.File.Exists(Path.Combine(ResolveWebRootPath(), "uploads", "chats", chatId.ToString("N"), fileName));
    }

    private async Task ProcessSupportMessageAsync(
        Chat chat,
        Guid senderId,
        Message message,
        CancellationToken cancellationToken)
    {
        if (!chat.IsSupport || senderId != chat.BuyerId)
        {
            return;
        }

        if (ContainsAdminRequest(message.Content))
        {
            if (chat.SupportStatus == SupportChatStatus.BotActive)
            {
                await EscalateSupportChatAsync(chat, cancellationToken);
            }

            return;
        }

        if (chat.SupportStatus == SupportChatStatus.AdminActive &&
            HasAdminBeenInactiveLongEnough(chat))
        {
            chat.SupportStatus = SupportChatStatus.BotActive;
            chat.AssignedAdminId = null;
        }

        if (chat.SupportStatus != SupportChatStatus.BotActive)
        {
            return;
        }

        if (message.Type != MessageType.Text || !string.IsNullOrWhiteSpace(message.AttachmentUrl))
        {
            await AddSupportBotMessageAsync(chat, SupportBotService.TextOnlyMessage, cancellationToken);
            return;
        }

        var history = await _dbContext.Messages
            .AsNoTracking()
            .Where(item => item.ChatId == chat.Id)
            .OrderBy(item => item.SentAtUtc)
            .Select(item => new SupportBotHistoryMessage(
                item.SenderId == chat.BuyerId,
                item.Content))
            .ToArrayAsync(cancellationToken);

        var reply = await _supportBot.GenerateReplyAsync(history, cancellationToken);
        if (string.IsNullOrWhiteSpace(reply))
        {
            await AddSupportBotMessageAsync(chat, SupportBotService.UnavailableMessage, cancellationToken);
            return;
        }

        if (reply.Contains(SupportBotService.HandoffMessage, StringComparison.OrdinalIgnoreCase))
        {
            reply = SupportBotService.OutOfScopeMessage;
        }

        await AddSupportBotMessageAsync(chat, reply, cancellationToken);
    }

    private static bool HasAdminBeenInactiveLongEnough(Chat chat)
    {
        if (!chat.AssignedAdminId.HasValue)
        {
            return false;
        }

        var lastAdminMessageAt = chat.Messages
            .Where(message => message.SenderId == chat.AssignedAdminId.Value)
            .OrderByDescending(message => message.SentAtUtc)
            .Select(message => (DateTime?)message.SentAtUtc)
            .FirstOrDefault();

        return lastAdminMessageAt.HasValue &&
               DateTime.UtcNow - lastAdminMessageAt.Value >= AdminInactivityTimeout;
    }

    private async Task EscalateSupportChatAsync(Chat chat, CancellationToken cancellationToken)
    {
        chat.SupportStatus = SupportChatStatus.NeedsAdmin;
        await AddSupportBotMessageAsync(chat, SupportBotService.HandoffMessage, cancellationToken);
    }

    private async Task<MessageResponse> AddSupportBotMessageAsync(
        Chat chat,
        string content,
        CancellationToken cancellationToken)
    {
        var message = new Message
        {
            ChatId = chat.Id,
            SenderId = chat.SellerId,
            Content = content,
            Type = MessageType.Text,
            SentAtUtc = DateTime.UtcNow,
            Chat = chat
        };

        _dbContext.Messages.Add(message);
        chat.LastMessageAtUtc = message.SentAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.Messages
            .AsNoTracking()
            .Include(item => item.Sender)
            .FirstOrDefaultAsync(item => item.Id == message.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Support bot message was not found.");

        var response = MarketplaceMapper.MapMessage(created);
        await _hubContext.Clients.Group(ChatHub.BuildChatGroup(chat.Id))
            .SendAsync("MessageReceived", response, cancellationToken);
        return response;
    }

    private static bool ContainsAdminRequest(string content)
    {
        string[] triggers =
        [
            "позови человека",
            "нужен оператор",
            "позови оператора",
            "подключи оператора",
            "подключите оператора",
            "свяжи с оператором",
            "свяжите с оператором",
            "хочу поговорить с оператором",
            "хочу связаться с оператором",
            "свяжи с админом",
            "свяжи с администратором",
            "подключи администратора",
            "подключите администратора",
            "хочу поговорить с администратором",
            "живой человек",
            "нужен человек",
            "call an operator",
            "connect to an operator",
            "connect me to an operator",
            "human agent",
            "real person",
            "talk to an admin",
            "потрібен оператор",
            "покличте оператора"
        ];

        return triggers.Any(trigger => content.Contains(trigger, StringComparison.OrdinalIgnoreCase));
    }

    private bool CanAccessChat(Chat chat, Guid currentUserId)
    {
        return chat.BuyerId == currentUserId ||
               chat.SellerId == currentUserId ||
               User.IsElevatedAdmin();
    }

    private bool TryEnsureSupportChatCanBeManaged(Chat chat, Guid currentUserId)
    {
        if (!chat.IsSupport || !User.IsElevatedAdmin())
        {
            return true;
        }

        if (chat.AssignedAdminId.HasValue && chat.AssignedAdminId != currentUserId)
        {
            return false;
        }

        if (!chat.AssignedAdminId.HasValue)
        {
            chat.AssignedAdminId = currentUserId;
        }

        chat.SupportStatus = SupportChatStatus.AdminActive;
        return true;
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
