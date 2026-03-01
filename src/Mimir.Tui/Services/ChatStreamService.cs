using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Mimir.Tui.Models;

namespace Mimir.Tui.Services;

/// <summary>
/// Manages the SignalR connection to ChatHub and streams LLM responses.
/// </summary>
internal sealed class ChatStreamService : IAsyncDisposable
{
    private readonly TuiSettings _settings;
    private readonly AuthenticationService _authService;
    private HubConnection? _connection;

    public ChatStreamService(IOptions<TuiSettings> options, AuthenticationService authService)
    {
        _settings = options.Value;
        _authService = authService;
    }

    /// <summary>
    /// Gets a value indicating whether the SignalR connection is active.
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Establishes the SignalR connection to the ChatHub.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_settings.HubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_authService.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        await _connection.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Sends a message and streams the response tokens from the LLM.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="content">The user's message content.</param>
    /// <param name="model">Optional model override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of chat tokens.</returns>
    public async IAsyncEnumerable<ChatTokenDto> StreamMessageAsync(
        string conversationId,
        string content,
        string? model,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to ChatHub. Call ConnectAsync first.");
        }

        var stream = _connection.StreamAsync<ChatTokenDto>(
            "SendMessage",
            conversationId,
            content,
            model,
            cancellationToken);

        await foreach (var token in stream.WithCancellation(cancellationToken))
        {
            yield return token;
        }
    }

    /// <summary>
    /// Disconnects from the ChatHub.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_connection is not null)
        {
            await _connection.StopAsync();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
