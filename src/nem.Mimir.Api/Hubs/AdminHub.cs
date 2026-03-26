using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Api.Hubs;

[Authorize(Policy = "RequireAdmin")]
public sealed class AdminHub : Hub
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AdminHub> _logger;

    public AdminHub(ICurrentUserService currentUserService, ILogger<AdminHub> logger)
    {
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            Context.Abort();
            return;
        }

        _logger.LogInformation("User {UserId} connected to AdminHub. ConnectionId: {ConnectionId}",
            userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogWarning(exception, "User disconnected from AdminHub with error. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("User disconnected from AdminHub. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task BroadcastSystemNotification(string message)
    {
        await Clients.All.SendAsync("SystemNotification", new
        {
            message,
            timestamp = DateTimeOffset.UtcNow
        });

        _logger.LogInformation("Stub system notification broadcast to admin clients");
    }

    public async Task SubscribeToUserActivity()
    {
        const string groupName = "user-activity";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} subscribed to {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task UnsubscribeFromUserActivity()
    {
        const string groupName = "user-activity";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} unsubscribed from {GroupName}", Context.ConnectionId, groupName);
    }
}
