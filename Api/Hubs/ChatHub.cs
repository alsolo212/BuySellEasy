using Api.Extensions;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
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

    public Task JoinChat(Guid chatId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, BuildChatGroup(chatId));
    }

    public Task LeaveChat(Guid chatId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildChatGroup(chatId));
    }
}
