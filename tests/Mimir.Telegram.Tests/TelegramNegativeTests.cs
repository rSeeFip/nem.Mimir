using Microsoft.Extensions.Logging;
using Mimir.Telegram.Handlers;
using Mimir.Telegram.Services;
using NSubstitute;
using Shouldly;
using global::Telegram.Bot;
using global::Telegram.Bot.Types;
using global::Telegram.Bot.Types.Enums;
using global::Telegram.Bot.Requests;

namespace Mimir.Telegram.Tests;

/// <summary>
/// Comprehensive negative tests for Telegram bot components: command handling edge cases,
/// authentication failures, state management boundary conditions, and message handler failures.
/// </summary>
public sealed class TelegramNegativeTests
{
    private readonly MimirApiClient _apiClient;
    private readonly UserStateManager _stateManager = new();
    private readonly AuthenticationService _authService;
    private readonly ILogger<CommandHandler> _cmdLogger = Substitute.For<ILogger<CommandHandler>>();
    private readonly ILogger<MessageHandler> _msgLogger = Substitute.For<ILogger<MessageHandler>>();
    private readonly ITelegramBotClient _bot = Substitute.For<ITelegramBotClient>();

    public TelegramNegativeTests()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        _apiClient = new MimirApiClient(httpClient, Substitute.For<ILogger<MimirApiClient>>());

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var authSettings = Microsoft.Extensions.Options.Options.Create(new Configuration.TelegramSettings
        {
            AuthCodeExpiryMinutes = 5,
            KeycloakUrl = "http://localhost:8080/realms/mimir"
        });
        _authService = new AuthenticationService(httpClientFactory, authSettings, Substitute.For<ILogger<AuthenticationService>>());
    }

    // ══════════════════════════════════════════════════════════════════
    // CommandHandler.IsCommand — edge cases
    // ══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("/", false)]
    [InlineData("//", false)]
    [InlineData("hello /start", false)]
    public void IsCommand_EdgeCases_ReturnsExpected(string? text, bool expected)
    {
        CommandHandler.IsCommand(text).ShouldBe(expected);
    }

    // ══════════════════════════════════════════════════════════════════
    // CommandHandler.EscapeHtml — edge cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void EscapeHtml_EmptyString_ReturnsEmpty()
    {
        CommandHandler.EscapeHtml("").ShouldBe("");
    }

    [Fact]
    public void EscapeHtml_NullInput_ThrowsOrReturnsNull()
    {
        // Expected: either ArgumentNullException or returns empty/null
        try
        {
            var result = CommandHandler.EscapeHtml(null!);
            // If it doesn't throw, result should be null or empty
            (result == null || result == "").ShouldBeTrue("Expected null or empty string");
        }
        catch (NullReferenceException)
        {
            // Acceptable — null input causes NRE
        }
        catch (ArgumentNullException)
        {
            // Also acceptable
        }
    }

    [Fact]
    public void EscapeHtml_MultipleSpecialChars_EscapesAll()
    {
        var result = CommandHandler.EscapeHtml("<script>alert(\"hello & goodbye\")</script>");

        result.ShouldContain("&lt;");
        result.ShouldContain("&gt;");
        result.ShouldContain("&amp;");
        result.ShouldContain("&quot;");
    }

    // ══════════════════════════════════════════════════════════════════
    // CommandHandler — authentication required commands
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task HandleAsync_ListCommand_WhenNotAuthenticated_PromptsAuth()
    {
        var sut = new CommandHandler(_apiClient, _stateManager, _authService, _cmdLogger);
        var message = CreateMessage("/list");

        await sut.HandleAsync(_bot, message, CancellationToken.None);

        var sentText = GetSentMessageText();
        sentText.ShouldNotBeNull();
        sentText.ShouldContain("authenticate first");
    }

    [Fact]
    public async Task HandleAsync_ModelCommand_WhenNotAuthenticated_PromptsAuth()
    {
        var sut = new CommandHandler(_apiClient, _stateManager, _authService, _cmdLogger);
        var message = CreateMessage("/model phi-4-mini");

        await sut.HandleAsync(_bot, message, CancellationToken.None);

        var sentText = GetSentMessageText();
        sentText.ShouldNotBeNull();
        sentText.ShouldContain("authenticate first");
    }

    [Fact]
    public async Task HandleAsync_EmptyTextMessage_DoesNotThrow()
    {
        var sut = new CommandHandler(_apiClient, _stateManager, _authService, _cmdLogger);
        var message = CreateMessage("");

        // Should not throw on empty text
        await sut.HandleAsync(_bot, message, CancellationToken.None);
    }

    // ══════════════════════════════════════════════════════════════════
    // UserStateManager — boundary conditions
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void UserStateManager_GetState_UnknownUser_ReturnsDefault()
    {
        var manager = new UserStateManager();

        var isAuthenticated = manager.IsAuthenticated(999);

        isAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public void UserStateManager_SetAuthenticated_ThenCheck_ReturnsTrue()
    {
        var manager = new UserStateManager();
        manager.SetAuthenticated(100, "test-token");

        var isAuthenticated = manager.IsAuthenticated(100);

        isAuthenticated.ShouldBeTrue();
    }

    [Fact]
    public void UserStateManager_NegativeUserId_DoesNotThrow()
    {
        var manager = new UserStateManager();

        // Negative user ID should not throw
        var isAuthenticated = manager.IsAuthenticated(-1);
        isAuthenticated.ShouldBeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private string? GetSentMessageText()
    {
        var calls = _bot.ReceivedCalls();
        foreach (var call in calls)
        {
            var args = call.GetArguments();
            if (args.Length > 0 && args[0] is SendMessageRequest request)
            {
                return request.Text;
            }
        }
        return null;
    }

    private static Message CreateMessage(string text)
    {
        return new Message
        {
            Text = text,
            Chat = new Chat { Id = 100, Type = ChatType.Private },
            From = new User { Id = 200, IsBot = false, FirstName = "Test" },
            Date = DateTime.UtcNow
        };
    }
}
