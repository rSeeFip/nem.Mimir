using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Api.Hubs;

[Authorize]
public sealed class CollaborationHub : Hub
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CollaborationHub> _logger;

    public CollaborationHub(ICurrentUserService currentUserService, ILogger<CollaborationHub> logger)
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

        _logger.LogInformation("User {UserId} connected to CollaborationHub. ConnectionId: {ConnectionId}",
            userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogWarning(exception, "User disconnected from CollaborationHub with error. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("User disconnected from CollaborationHub. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinChannel(string channelId)
    {
        var groupName = $"channel:{channelId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} joined collaboration channel group {GroupName}",
            Context.ConnectionId, groupName);
    }

    public async Task LeaveChannel(string channelId)
    {
        var groupName = $"channel:{channelId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} left collaboration channel group {GroupName}",
            Context.ConnectionId, groupName);
    }

    public async Task SendChannelMessage(string channelId, string content)
    {
        var userId = _currentUserService.UserId;
        var groupName = $"channel:{channelId}";

        await Clients.Group(groupName).SendAsync("ChannelMessage", new
        {
            channelId,
            content,
            userId,
            timestamp = DateTimeOffset.UtcNow
        });

        _logger.LogInformation("Stub channel message broadcast to {GroupName} by user {UserId}",
            groupName, userId);
    }

    public async Task SendTypingIndicator(string channelId)
    {
        var userId = _currentUserService.UserId;
        var groupName = $"channel:{channelId}";

        await Clients.Group(groupName).SendAsync("TypingIndicator", new
        {
            channelId,
            userId,
            timestamp = DateTimeOffset.UtcNow
        });

        _logger.LogInformation("Stub typing indicator broadcast to {GroupName} by user {UserId}",
            groupName, userId);
    }

    public async Task JoinNote(string noteId)
    {
        var groupName = $"note:{noteId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} joined collaboration note group {GroupName}",
            Context.ConnectionId, groupName);
    }

    public async Task LeaveNote(string noteId)
    {
        var groupName = $"note:{noteId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} left collaboration note group {GroupName}",
            Context.ConnectionId, groupName);
    }

    public async Task UpdatePresence(string status)
    {
        var userId = _currentUserService.UserId;

        await Clients.All.SendAsync("PresenceUpdated", new
        {
            userId,
            status,
            timestamp = DateTimeOffset.UtcNow
        });

        _logger.LogInformation("Stub presence update broadcast for user {UserId} with status {Status}", userId, status);
    }
}
