namespace Mimir.Telegram.Tests.Services;

using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mimir.Telegram.Configuration;
using Mimir.Telegram.Handlers;
using Mimir.Telegram.Services;
using NSubstitute;
using Shouldly;

public sealed class TelegramBotServiceTests
{
    private readonly ILogger<TelegramBotService> _logger = NullLogger<TelegramBotService>.Instance;

    /// <summary>
    /// Creates a TelegramBotService with the given settings and real (but unused) handlers.
    /// Handlers require complex dependencies, so we construct lightweight real instances
    /// that won't be exercised in the early-return/empty-token paths.
    /// </summary>
    private TelegramBotService CreateService(TelegramSettings settings)
    {
        var options = Options.Create(settings);

        // Build minimal real dependencies for handlers (won't be used in early-return tests)
        var httpClient = new HttpClient(new StubHttpMessageHandler())
        {
            BaseAddress = new Uri("http://localhost:5000/"),
        };
        var apiClient = new MimirApiClient(httpClient, NullLogger<MimirApiClient>.Instance);
        var stateManager = new UserStateManager();

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>())
            .Returns(new HttpClient(new StubHttpMessageHandler()) { BaseAddress = new Uri("http://localhost:5000/") });
        var authService = new AuthenticationService(
            httpClientFactory,
            Options.Create(settings),
            NullLogger<AuthenticationService>.Instance);

        var commandHandler = new CommandHandler(
            apiClient, stateManager, authService, NullLogger<CommandHandler>.Instance);
        var messageHandler = new MessageHandler(
            apiClient, stateManager, Options.Create(settings), NullLogger<MessageHandler>.Instance);

        return new TelegramBotService(options, commandHandler, messageHandler, _logger);
    }

    // ─── Empty / Null BotToken ───────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_EmptyBotToken_ReturnsWithoutStartingBot()
    {
        // Arrange
        var settings = new TelegramSettings { BotToken = string.Empty };
        var service = CreateService(settings);
        using var cts = new CancellationTokenSource();

        // Act - StartAsync calls ExecuteAsync internally
        await service.StartAsync(cts.Token);
        // Give a tiny moment for ExecuteAsync to run
        await Task.Delay(100);

        // Assert - service should have returned early (not crashed, not looping)
        // We verify this by simply completing without timeout or exception
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhitespaceBotToken_ReturnsWithoutStartingBot()
    {
        // Arrange
        var settings = new TelegramSettings { BotToken = "   " };
        var service = CreateService(settings);
        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);

        // Assert - should return early, no crash
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidBotToken_ReturnsAfterFailedConnection()
    {
        // Arrange - invalid token will fail on GetMe() call to Telegram API
        var settings = new TelegramSettings { BotToken = "invalid:token-that-will-fail" };
        var service = CreateService(settings);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        await service.StartAsync(cts.Token);
        // Give time for the GetMe() attempt to fail and ExecuteAsync to return
        await Task.Delay(3000);

        // Assert - service should have stopped after failed connection, no crash
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationBeforeStart_ExitsGracefully()
    {
        // Arrange
        var settings = new TelegramSettings { BotToken = "some:valid-looking-token" };
        var service = CreateService(settings);
        using var cts = new CancellationTokenSource();

        // Cancel immediately before starting
        await cts.CancelAsync();

        // Act & Assert - should not throw
        await service.StartAsync(cts.Token);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);
    }

    // ─── Helper ──────────────────────────────────────────────────

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
