using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mimir.Api.Hubs;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Sanitization;
using Mimir.Domain.Entities;
using Mimir.Infrastructure.LiteLlm;
using NSubstitute;
using Shouldly;

namespace Mimir.Api.Tests.Hubs;

public sealed class ChatHubTests : IDisposable
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly IContextWindowService _contextWindowService;
    private readonly LlmRequestQueue _requestQueue;
    private readonly ILogger<ChatHub> _logger;
    private readonly ISanitizationService _sanitizationService;
    private readonly ChatHub _hub;

    public ChatHubTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _llmService = Substitute.For<ILlmService>();
        _contextWindowService = Substitute.For<IContextWindowService>();
        _requestQueue = new LlmRequestQueue(NullLogger<LlmRequestQueue>.Instance);
        _logger = NullLogger<ChatHub>.Instance;
        _sanitizationService = Substitute.For<ISanitizationService>();

        // Default: sanitize returns input as-is
        _sanitizationService.SanitizeUserInput(Arg.Any<string>()).Returns(ci => ci.Arg<string>());

        _hub = new ChatHub(
            _repository,
            _currentUserService,
            _unitOfWork,
            _llmService,
            _contextWindowService,
            _requestQueue,
            _logger,
            _sanitizationService);

        // Set up mock Hub context and groups
        var mockContext = Substitute.For<HubCallerContext>();
        mockContext.ConnectionId.Returns("test-connection-id");

        var mockGroups = Substitute.For<IGroupManager>();

        // Assign via the Hub base class properties (they have public setters in the test context)
        SetHubContext(_hub, mockContext, mockGroups);
    }

    public void Dispose()
    {
        _requestQueue.Dispose();
    }

    /// <summary>
    /// Sets the Context and Groups properties on a Hub instance using reflection,
    /// since they are not directly settable in test code.
    /// </summary>
    private static void SetHubContext(Hub hub, HubCallerContext context, IGroupManager groups)
    {
        // Hub.Context and Hub.Groups have internal setters accessible via the Clients/Context pattern
        var hubType = typeof(Hub);

        var contextProperty = hubType.GetProperty(nameof(Hub.Context))!;
        contextProperty.SetValue(hub, context);

        var groupsProperty = hubType.GetProperty(nameof(Hub.Groups))!;
        groupsProperty.SetValue(hub, groups);

        var clientsProperty = hubType.GetProperty(nameof(Hub.Clients))!;
        clientsProperty.SetValue(hub, Substitute.For<IHubCallerClients>());
    }

    // ── SendMessage: User not authenticated ────────────────────────────────

    [Fact]
    public async Task SendMessage_NullUserId_ThrowsForbiddenAccessException()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);

        // Act & Assert
        var exception = await Should.ThrowAsync<ForbiddenAccessException>(async () =>
        {
            await foreach (var _ in _hub.SendMessage("some-id", "hello", null, CancellationToken.None))
            {
                // consume
            }
        });

        exception.Message.ShouldBe("User is not authenticated.");
    }

    // ── SendMessage: Invalid conversation ID ───────────────────────────────

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz")]
    public async Task SendMessage_InvalidConversationId_YieldsErrorToken(string badConversationId)
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        _currentUserService.UserId.Returns(userId);

        // Act
        var tokens = await CollectTokensAsync(
            _hub.SendMessage(badConversationId, "hello", null, CancellationToken.None));

        // Assert
        tokens.ShouldHaveSingleItem();
        tokens[0].Token.ShouldBe("Invalid conversation ID.");
        tokens[0].IsComplete.ShouldBeTrue();
        tokens[0].QueuePosition.ShouldBeNull();
    }

    // ── SendMessage: Empty/whitespace content ──────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task SendMessage_EmptyOrWhitespaceContent_YieldsErrorToken(string emptyContent)
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        _currentUserService.UserId.Returns(userId);
        var conversationId = Guid.NewGuid().ToString();

        // Act
        var tokens = await CollectTokensAsync(
            _hub.SendMessage(conversationId, emptyContent, null, CancellationToken.None));

        // Assert
        tokens.ShouldHaveSingleItem();
        tokens[0].Token.ShouldBe("Message content is required.");
        tokens[0].IsComplete.ShouldBeTrue();
    }

    [Fact]
    public async Task SendMessage_NullContent_YieldsErrorToken()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        _currentUserService.UserId.Returns(userId);
        var conversationId = Guid.NewGuid().ToString();

        // Act
        var tokens = await CollectTokensAsync(
            _hub.SendMessage(conversationId, null!, null, CancellationToken.None));

        // Assert
        tokens.ShouldHaveSingleItem();
        tokens[0].Token.ShouldBe("Message content is required.");
        tokens[0].IsComplete.ShouldBeTrue();
    }

    // ── SendMessage: Conversation not found ────────────────────────────────

    [Fact]
    public async Task SendMessage_ConversationNotFound_YieldsErrorToken()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        _currentUserService.UserId.Returns(userId);
        var conversationId = Guid.NewGuid();

        _repository.GetWithMessagesAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        // Act
        var tokens = await CollectTokensAsync(
            _hub.SendMessage(conversationId.ToString(), "hello", null, CancellationToken.None));

        // Assert
        tokens.ShouldHaveSingleItem();
        tokens[0].Token.ShouldBe("Conversation not found.");
        tokens[0].IsComplete.ShouldBeTrue();
    }

    // ── SendMessage: Conversation owned by different user ──────────────────

    [Fact]
    public async Task SendMessage_ConversationOwnedByDifferentUser_YieldsErrorToken()
    {
        // Arrange
        var requestingUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        _currentUserService.UserId.Returns(requestingUserId.ToString());

        var conversationId = Guid.NewGuid();
        var conversation = Conversation.Create(ownerUserId, "Other user's conversation");

        _repository.GetWithMessagesAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        // Act
        var tokens = await CollectTokensAsync(
            _hub.SendMessage(conversationId.ToString(), "hello", null, CancellationToken.None));

        // Assert
        tokens.ShouldHaveSingleItem();
        tokens[0].Token.ShouldBe("Conversation not found.");
        tokens[0].IsComplete.ShouldBeTrue();
    }

    // ── OnConnectedAsync: null/empty user ID aborts ────────────────────────

    [Fact]
    public async Task OnConnectedAsync_NullUserId_AbortsConnection()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);
        var mockContext = Substitute.For<HubCallerContext>();
        mockContext.ConnectionId.Returns("conn-123");
        SetHubContext(_hub, mockContext, Substitute.For<IGroupManager>());

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        mockContext.Received(1).Abort();
    }

    [Fact]
    public async Task OnConnectedAsync_EmptyUserId_AbortsConnection()
    {
        // Arrange
        _currentUserService.UserId.Returns(string.Empty);
        var mockContext = Substitute.For<HubCallerContext>();
        mockContext.ConnectionId.Returns("conn-456");
        SetHubContext(_hub, mockContext, Substitute.For<IGroupManager>());

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        mockContext.Received(1).Abort();
    }

    [Fact]
    public async Task OnConnectedAsync_WhitespaceUserId_AbortsConnection()
    {
        // Arrange
        _currentUserService.UserId.Returns("   ");
        var mockContext = Substitute.For<HubCallerContext>();
        mockContext.ConnectionId.Returns("conn-789");
        SetHubContext(_hub, mockContext, Substitute.For<IGroupManager>());

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        mockContext.Received(1).Abort();
    }

    // ── OnDisconnectedAsync: with exception does not throw ─────────────────

    [Fact]
    public async Task OnDisconnectedAsync_WithException_DoesNotThrow()
    {
        // Arrange
        var testException = new InvalidOperationException("connection lost");

        // Act & Assert — should not throw, just log
        await Should.NotThrowAsync(async () =>
            await _hub.OnDisconnectedAsync(testException));
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithNullException_DoesNotThrow()
    {
        // Act & Assert — normal disconnect path
        await Should.NotThrowAsync(async () =>
            await _hub.OnDisconnectedAsync(null));
    }

    // ── SendMessage: Conversation ID is valid GUID but repo returns null ───

    [Fact]
    public async Task SendMessage_RepositoryReturnsNull_YieldsConversationNotFoundToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        var conversationGuid = Guid.NewGuid();

        _repository.GetWithMessagesAsync(conversationGuid, Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        // Act
        var tokens = await CollectTokensAsync(
            _hub.SendMessage(conversationGuid.ToString(), "test message", null, CancellationToken.None));

        // Assert
        tokens.ShouldHaveSingleItem();
        tokens[0].Token.ShouldBe("Conversation not found.");
        tokens[0].IsComplete.ShouldBeTrue();
    }

    // ── SendMessage: User ID from service is not a valid GUID ──────────────

    [Fact]
    public async Task SendMessage_UserIdNotValidGuid_ThrowsFormatException()
    {
        // Arrange — UserId is non-null but not a parseable GUID
        _currentUserService.UserId.Returns("not-a-guid-user-id");

        // Act & Assert — Guid.Parse will throw
        await Should.ThrowAsync<FormatException>(async () =>
        {
            await foreach (var _ in _hub.SendMessage(
                Guid.NewGuid().ToString(), "hello", null, CancellationToken.None))
            {
                // consume
            }
        });
    }

    // ── Helper ─────────────────────────────────────────────────────────────

    private static async Task<List<ChatToken>> CollectTokensAsync(IAsyncEnumerable<ChatToken> source)
    {
        var result = new List<ChatToken>();
        await foreach (var token in source)
        {
            result.Add(token);
        }

        return result;
    }
}
