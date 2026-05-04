using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Sanitization;
using nem.Mimir.Telegram.Configuration;
using nem.Mimir.Telegram.Handlers;
using nem.Mimir.Telegram.Services;
using NSubstitute;
using Shouldly;
using global::Telegram.Bot;
using global::Telegram.Bot.Types;
using global::Telegram.Bot.Types.Enums;
using global::Telegram.Bot.Requests;

namespace nem.Mimir.Telegram.Tests.Handlers;

public sealed class MessageHandlerTests
{
    private readonly MimirApiClient _apiClient;
    private readonly UserStateManager _stateManager = new();
    private readonly IOptions<TelegramSettings> _settings;
    private readonly ILogger<MessageHandler> _logger = Substitute.For<ILogger<MessageHandler>>();
    private readonly MessageHandler _sut;
    private readonly ITelegramBotClient _bot = Substitute.For<ITelegramBotClient>();

    public MessageHandlerTests()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        _apiClient = new MimirApiClient(httpClient, Substitute.For<ILogger<MimirApiClient>>());

        _settings = Options.Create(new TelegramSettings
        {
            StreamingUpdateIntervalMs = 500
        });

        _sut = new MessageHandler(_apiClient, _stateManager, _settings, _logger,
            Substitute.For<ISanitizationService>(),
            Options.Create(new SanitizationOptions { DefaultMode = SanitizationMode.Log }));
    }

    [Fact]
    public async Task HandleAsync_WhenNotAuthenticated_PromptsAuth()
    {
        var message = CreateMessage("Hello AI");

        await _sut.HandleAsync(_bot, message, CancellationToken.None);

        var sentText = GetSentMessageText();
        sentText.ShouldNotBeNull();
        sentText.ShouldContain("authenticate first");
    }

    [Fact]
    public async Task HandleAsync_WhenNoActiveConversation_PromptsToCreateOne()
    {
        _stateManager.SetAuthenticated(200, "test-token");
        var message = CreateMessage("Hello AI");

        await _sut.HandleAsync(_bot, message, CancellationToken.None);

        var sentText = GetSentMessageText();
        sentText.ShouldNotBeNull();
        sentText.ShouldContain("No active conversation");
    }

    [Fact]
    public async Task HandleAsync_WhenEmptyText_DoesNothing()
    {
        var message = CreateMessage("");

        await _sut.HandleAsync(_bot, message, CancellationToken.None);

        var sentText = GetSentMessageText();
        sentText.ShouldBeNull("No message should be sent for empty text");
    }

    [Fact]
    public async Task HandleAsync_SendsToCorrectChat()
    {
        var message = CreateMessage("Hello AI");

        await _sut.HandleAsync(_bot, message, CancellationToken.None);

        var chatId = GetSentMessageChatId();
        chatId.ShouldNotBeNull();
        chatId.Identifier.ShouldBe(100);
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

    [Fact]
    public async Task HandleAsync_SanitizationCalled_OnUserInput()
    {
        var sanitizationService = Substitute.For<ISanitizationService>();
        sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(false);
        sanitizationService.SanitizeUserInput(Arg.Any<string>()).Returns("sanitized text");

        var sut = CreateHandlerWithSanitization(sanitizationService,
            new SanitizationOptions
            {
                EnforcementEnabled = true,
                DefaultMode = SanitizationMode.Sanitize
            });

        var bot = CreateBotWithThinkingMessageMock();
        _stateManager.SetAuthenticated(200, "test-token");
        _stateManager.SetCurrentConversation(200, Guid.NewGuid(), "Test Conversation");
        var message = CreateMessage("Hello AI");

        await sut.HandleAsync(bot, message, CancellationToken.None);

        sanitizationService.Received(1).SanitizeUserInput("Hello AI");
    }

    [Fact]
    public async Task HandleAsync_BlockMode_ReturnErrorMessage_WhenSuspiciousContent()
    {
        var sanitizationService = Substitute.For<ISanitizationService>();
        sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        var sut = CreateHandlerWithSanitization(sanitizationService,
            new SanitizationOptions
            {
                EnforcementEnabled = true,
                DefaultMode = SanitizationMode.Block
            });

        _stateManager.SetAuthenticated(200, "test-token");
        _stateManager.SetCurrentConversation(200, Guid.NewGuid(), "Test Conversation");
        var message = CreateMessage("<script>alert('xss')</script>");

        await sut.HandleAsync(_bot, message, CancellationToken.None);

        var sentText = GetSentMessageText();
        sentText.ShouldNotBeNull();
        sentText.ShouldContain("blocked");
    }

    [Fact]
    public async Task HandleAsync_SanitizeMode_StripsSuspiciousContent_AndContinues()
    {
        var sanitizationService = Substitute.For<ISanitizationService>();
        sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);
        sanitizationService.SanitizeUserInput(Arg.Any<string>()).Returns("clean text");

        var sut = CreateHandlerWithSanitization(sanitizationService,
            new SanitizationOptions
            {
                EnforcementEnabled = true,
                DefaultMode = SanitizationMode.Sanitize
            });

        var bot = CreateBotWithThinkingMessageMock();
        _stateManager.SetAuthenticated(200, "test-token");
        _stateManager.SetCurrentConversation(200, Guid.NewGuid(), "Test Conversation");
        var message = CreateMessage("<script>bad</script> clean text");

        await sut.HandleAsync(bot, message, CancellationToken.None);

        sanitizationService.Received(1).SanitizeUserInput(Arg.Any<string>());
        var calls = bot.ReceivedCalls().ToList();
        var blockMessageSent = calls
            .Select(c => c.GetArguments())
            .Where(a => a.Length > 0 && a[0] is SendMessageRequest)
            .Select(a => ((SendMessageRequest)a[0]!).Text!)
            .Any(t => t.Contains("blocked", StringComparison.OrdinalIgnoreCase));
        blockMessageSent.ShouldBeFalse();
    }

    private MessageHandler CreateHandlerWithSanitization(
        ISanitizationService sanitizationService,
        SanitizationOptions options)
    {
        return new MessageHandler(
            _apiClient,
            _stateManager,
            _settings,
            _logger,
            sanitizationService,
            Options.Create(options));
    }

    private static ITelegramBotClient CreateBotWithThinkingMessageMock()
    {
        var bot = Substitute.For<ITelegramBotClient>();
        var thinkingMessage = new Message { Chat = new Chat { Id = 100 }, Date = DateTime.UtcNow };
        bot.SendRequest<Message>(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(thinkingMessage));
        return bot;
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
