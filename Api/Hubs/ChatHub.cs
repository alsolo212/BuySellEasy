using Api.Extensions;
using Domain.Entities;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ProductDbContext _dbContext;

    public ChatHub(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static string BuildChatGroup(Guid chatId) => $"chat:{chatId:N}";

    public static string BuildUserGroup(Guid userId) => $"user:{userId:N}";

    public static string BuildAdminsGroup() => "role:admins";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User ?? throw new UnauthorizedAccessException("User is not authenticated.");
        var userId = user
            .GetRequiredUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroup(userId));
        if (user.IsInRole(IdentitySeed.Admin) || user.IsInRole(IdentitySeed.SuperAdmin))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildAdminsGroup());
        }
        await base.OnConnectedAsync();
    }

    public async Task JoinChat(Guid chatId)
    {
        await GetAuthorizedChatAsync(chatId);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            BuildChatGroup(chatId),
            Context.ConnectionAborted);
    }

    public async Task LeaveChat(Guid chatId)
    {
        await GetAuthorizedChatAsync(chatId);
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            BuildChatGroup(chatId),
            Context.ConnectionAborted);
    }

    private async Task<Chat> GetAuthorizedChatAsync(Guid chatId)
    {
        var user = Context.User ?? throw new HubException("User is not authenticated.");
        var userId = user.GetRequiredUserId();
        var chat = await _dbContext.Chats
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == chatId, Context.ConnectionAborted);

        if (chat is null)
        {
            throw new HubException("Chat was not found.");
        }

        if (chat.BuyerId != userId &&
            chat.SellerId != userId &&
            !user.IsElevatedAdmin())
        {
            throw new HubException("You cannot access this chat.");
        }

        return chat;
    }
}
