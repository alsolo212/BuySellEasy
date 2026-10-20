using Api.Hubs;
using Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

public sealed class OrderRealtimeNotifier
{
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly ILogger<OrderRealtimeNotifier> _logger;

    public OrderRealtimeNotifier(
        IHubContext<OrderHub> hubContext,
        ILogger<OrderRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyAsync(
        Order order,
        string changeType = "updated",
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Groups(new[]
                {
                    OrderHub.BuildUserGroup(order.BuyerId),
                    OrderHub.BuildUserGroup(order.SellerId)
                })
                .SendAsync(
                    "OrderChanged",
                    new OrderChangedNotification(order.Id, changeType),
                    cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A websocket failure must never turn a successful order/payment operation into an API failure.
            _logger.LogWarning(exception, "Unable to publish realtime update for order {OrderId}.", order.Id);
        }
    }
}

public sealed record OrderChangedNotification(Guid OrderId, string ChangeType);
