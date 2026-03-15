using Microsoft.Extensions.Logging;
using nem.Mimir.Telegram.Handlers;
using nem.Mimir.Telegram.Services;
using NSubstitute;
using Shouldly;
using global::Telegram.Bot;
using global::Telegram.Bot.Types;
using global::Telegram.Bot.Types.Enums;
using global::Telegram.Bot.Requests;

namespace nem.Mimir.Telegram.Tests.Handlers;

public sealed class CommandHandlerTests
{
    private readonly MimirApiClient _apiClient;
    private readonly UserStateManager _stateManager = new();
    private readonly AuthenticationService _authService;
    private readonly ILogger<CommandHandler> _logger = Substitute.For<ILogger<CommandHandler>>();
    private readonly CommandHandler _sut;
    private readonly ITelegramBotClient _bot = Substitute.For<ITelegramBotClient>();

    public CommandHandlerTests()
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

        _sut = new CommandHandler(_apiClient, _stateManager, _authService, _logger);
    }

    [Theory]
    [InlineData("/start", true)]
    [InlineData("/help", true)]
    [InlineData("/new", true)]
    [InlineData("/list", true)]
    [InlineData("/model", true)]
    [InlineData("hello", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsCommand_IdentifiesCommandsCorrectly(string? text, bool expected)
    {
        CommandHandler.IsCommand(text).ShouldBe(expected);
    }

    [Fact]
    public async Task HandleAsync_Help_SendsHelpMessage()
    {
        var message = CreateMessage("/help");

        await _sut.HandleAsync(_bot, message, CancellationToken.None);

        var sentText = GetSentMessageText();
        sentText.ShouldNotBeNull();
        sentText.ShouldContain("Mimir Bot Commands");
    }

    [Fact]
    public async Task HandleAsync_Start_WhenNotAuthenticated_GeneratesAuthCode()
    {
        var message = CreateMessage("/start");

        await _sut.HandleAsync(_bot, message, CancellationToken.None);

        var sentText = GetSentMessageText();
        sentText.ShouldNotBeNull();
        sentText.ShouldContain("Welcome to Mimir");
    }

    [Fact]
    public async Task HandleAsync_Start_WhenAlreadyAuthenticated_InformsUser()
    {
        _stateManager.SetAuthenticated(200, "token");
        var message = CreateMessage("/start");

        await _sut.HandleAsync(_bot, message, CancellationToken.None);

        var sentText = GetSentMessageText();
        sentText.ShouldNotBeNull();
        sentText.ShouldContain("already authenticated");
    }

    [Fact]
    public async Task HandleAsync_UnknownCommand_SendsUnknownMessage()
    {
        var message = CreateMessage("/unknown");

        await _sut.HandleAsync(_bot, message, CancellationToken.None);

        var sentText = GetSentMessageText();
        sentText.ShouldNotBeNull();
        sentText.ShouldContain("Unknown command");
    }

    [Fact]
    public async Task HandleAsync_New_WhenNotAuthenticated_PromptsAuth()
    {
        var message = CreateMessage("/new");

        await _sut.HandleAsync(_bot, message, CancellationToken.None);

        var sentText = GetSentMessageText();
        sentText.ShouldNotBeNull();
        sentText.ShouldContain("authenticate first");
    }

    [Fact]
    public async Task HandleAsync_Help_SendsToCorrectChat()
    {
        var message = CreateMessage("/help");

        await _sut.HandleAsync(_bot, message, CancellationToken.None);

        var chatId = GetSentMessageChatId();
        chatId.ShouldNotBeNull();
        chatId.Identifier.ShouldBe(100);
    }

    [Theory]
    [InlineData("&", "&amp;")]
    [InlineData("<script>", "&lt;script&gt;")]
    [InlineData("\"hello\"", "&quot;hello&quot;")]
    [InlineData("plain text", "plain text")]
    public void EscapeHtml_EscapesCorrectly(string input, string expected)
    {
        CommandHandler.EscapeHtml(input).ShouldBe(expected);
    }

    /// <summary>
    /// Extracts the text from the last SendRequest call (which underlies the SendMessage extension method).
    /// SendMessage is an extension method and cannot be intercepted by NSubstitute directly,
    /// so we inspect the actual interface call: SendRequest&lt;Message&gt;(SendMessageRequest, CancellationToken).
    /// </summary>
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

    /// <summary>
    /// Extracts the ChatId from the last SendRequest call.
    /// </summary>
    private ChatId? GetSentMessageChatId()
    {
        var calls = _bot.ReceivedCalls();
        foreach (var call in calls)
        {
            var args = call.GetArguments();
            if (args.Length > 0 && args[0] is SendMessageRequest request)
            {
                return request.ChatId;
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
