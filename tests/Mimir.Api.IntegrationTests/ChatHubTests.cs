using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using Shouldly;

namespace Mimir.Api.IntegrationTests;

/// <summary>
/// Integration tests for the <see cref="Hubs.ChatHub"/> SignalR hub.
/// Verifies endpoint existence, authentication enforcement, and connection lifecycle.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ChatHubTests : IAsyncLifetime
{
    private readonly MimirWebApplicationFactory _factory;
    private HubConnection? _connection;

    public ChatHubTests(MimirWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task HubEndpoint_WithoutToken_RejectsConnection()
    {
        // Arrange — connect to the hub without an access token
        var server = _factory.Server;

        _connection = new HubConnectionBuilder()
            .WithUrl($"{server.BaseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        // Act & Assert — should throw because the hub requires [Authorize]
        var exception = await Should.ThrowAsync<Exception>(
            () => _connection.StartAsync());

        // The server should reject with 401 or close the connection
        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task HubEndpoint_WithInvalidToken_RejectsConnection()
    {
        // Arrange — connect with a bogus token
        var server = _factory.Server;

        _connection = new HubConnectionBuilder()
            .WithUrl($"{server.BaseAddress}hubs/chat?access_token=invalid-jwt-token", options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        // Act & Assert — should throw because the token is invalid
        var exception = await Should.ThrowAsync<Exception>(
            () => _connection.StartAsync());

        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task HubNegotiateEndpoint_WithoutToken_Returns401()
    {
        // Arrange — SignalR negotiate endpoint should require auth
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HubEndpoint_Exists_DoesNotReturn404()
    {
        // Arrange — verify the hub endpoint is mapped (not a 404)
        var client = _factory.CreateClient();

        // Act — a GET to the hub endpoint won't work for WebSocket but should not 404
        var response = await client.PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);

        // Assert — should be 401 (auth required), NOT 404 (endpoint missing)
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }
}
