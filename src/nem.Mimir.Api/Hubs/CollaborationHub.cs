using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Application.Channels.Commands;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using System.Collections.Concurrent;
using Wolverine;

namespace nem.Mimir.Api.Hubs;

[Authorize]
public sealed class CollaborationHub : Hub
{
    private static readonly ConcurrentDictionary<string, (Guid UserId, string? UserName)> ConnectionUsers = new();
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> ChannelConnections = new();

    private readonly ICurrentUserService _currentUserService;
    private readonly IMessageBus _bus;
    private readonly IChannelRepository _channelRepository;
    private readonly ILogger<CollaborationHub> _logger;

    public CollaborationHub(
        ICurrentUserService currentUserService,
        IMessageBus bus,
        IChannelRepository channelRepository,
        ILogger<CollaborationHub> logger)
    {
        _currentUserService = currentUserService;
        _bus = bus;
        _channelRepository = channelRepository;
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

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            Context.Abort();
            return;
        }

        ConnectionUsers[Context.ConnectionId] = (parsedUserId, Context.User?.Identity?.Name);

        _logger.LogInformation("User {UserId} connected to CollaborationHub. ConnectionId: {ConnectionId}",
            userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ConnectionUsers.TryRemove(Context.ConnectionId, out _);

        foreach (var channelConnections in ChannelConnections.Values)
        {
            channelConnections.TryRemove(Context.ConnectionId, out _);
        }

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
        if (!Guid.TryParse(channelId, out var channelGuid))
            throw new HubException("Invalid channel id.");

        if (!ConnectionUsers.TryGetValue(Context.ConnectionId, out var userInfo))
            throw new HubException("User context is unavailable.");

        var channel = await _channelRepository
            .GetWithMembersAsync(ChannelTypedId.From(channelGuid), CancellationToken.None)
            .ConfigureAwait(false);

        if (channel is null || !channel.Members.Any(m => m.UserId == userInfo.UserId && m.LeftAt == null))
            throw new HubException("User is not a channel member.");

        var groupName = $"channel:{channelId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var groupConnections = ChannelConnections.GetOrAdd(channelId, _ => new ConcurrentDictionary<string, byte>());
        groupConnections[Context.ConnectionId] = 0;

        _logger.LogInformation("Connection {ConnectionId} joined collaboration channel group {GroupName}",
            Context.ConnectionId, groupName);
    }

    public async Task LeaveChannel(string channelId)
    {
        var groupName = $"channel:{channelId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        if (ChannelConnections.TryGetValue(channelId, out var groupConnections))
        {
            groupConnections.TryRemove(Context.ConnectionId, out _);
            if (groupConnections.IsEmpty)
            {
                ChannelConnections.TryRemove(channelId, out _);
            }
        }

        _logger.LogInformation("Connection {ConnectionId} left collaboration channel group {GroupName}",
            Context.ConnectionId, groupName);
    }

    public async Task<ChannelMessageDto> SendChannelMessage(string channelId, string content)
    {
        if (!Guid.TryParse(channelId, out var channelGuid))
            throw new HubException("Invalid channel id.");

        if (!ConnectionUsers.TryGetValue(Context.ConnectionId, out var userInfo))
            throw new HubException("User context is unavailable.");

        var channel = await _channelRepository
            .GetWithMembersAsync(ChannelTypedId.From(channelGuid), CancellationToken.None)
            .ConfigureAwait(false);

        if (channel is null || !channel.Members.Any(m => m.UserId == userInfo.UserId && m.LeftAt == null))
            throw new HubException("User is not a channel member.");

        var message = await _bus.InvokeAsync<ChannelMessageDto>(
            new SendChannelMessageCommand(channelGuid, content, null),
            Context.ConnectionAborted);

        var groupName = $"channel:{channelId}";

        await Clients.Group(groupName).SendAsync("ChannelMessage", message, Context.ConnectionAborted);

        _logger.LogInformation("Persisted and broadcast channel message {MessageId} to {GroupName} by user {UserId}",
            message.Id, groupName, userInfo.UserId);

        return message;
    }

    public async Task StartTyping(string channelId)
    {
        if (!ConnectionUsers.TryGetValue(Context.ConnectionId, out var userInfo))
            throw new HubException("User context is unavailable.");

        var groupName = $"channel:{channelId}";

        await Clients.OthersInGroup(groupName).SendAsync("UserTyping", new
        {
            channelId,
            userId = userInfo.UserId,
            userName = userInfo.UserName,
            timestamp = DateTimeOffset.UtcNow
        }, Context.ConnectionAborted);
    }

    public async Task StopTyping(string channelId)
    {
        if (!ConnectionUsers.TryGetValue(Context.ConnectionId, out var userInfo))
            throw new HubException("User context is unavailable.");

        var groupName = $"channel:{channelId}";

        await Clients.OthersInGroup(groupName).SendAsync("UserStoppedTyping", new
        {
            channelId,
            userId = userInfo.UserId,
            userName = userInfo.UserName,
            timestamp = DateTimeOffset.UtcNow
        }, Context.ConnectionAborted);
    }

    public Task<IReadOnlyList<PresenceUserDto>> GetPresence(string channelId)
    {
        if (!ChannelConnections.TryGetValue(channelId, out var groupConnections))
            return Task.FromResult<IReadOnlyList<PresenceUserDto>>([]);

        var users = groupConnections.Keys
            .Select(connectionId => ConnectionUsers.TryGetValue(connectionId, out var user) ? user : ((Guid UserId, string? UserName)?)null)
            .Where(user => user.HasValue)
            .Select(user => user!.Value)
            .GroupBy(user => user.UserId)
            .Select(group => new PresenceUserDto(group.Key, group.First().UserName, true))
            .ToList();

        return Task.FromResult<IReadOnlyList<PresenceUserDto>>(users);
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

public sealed record PresenceUserDto(Guid UserId, string? UserName, bool IsOnline);
