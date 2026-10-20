using Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize]
public sealed class OrderHub : Hub
{
    public static string BuildUserGroup(Guid userId) => $"order-user:{userId:N}";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User ?? throw new UnauthorizedAccessException("User is not authenticated.");
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroup(user.GetRequiredUserId()));
        await base.OnConnectedAsync();
    }
}
