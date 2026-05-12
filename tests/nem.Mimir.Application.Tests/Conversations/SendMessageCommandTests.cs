using AwesomeAssertions;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.Application.Conversations.Services;
using nem.Mimir.Application.Knowledge;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class SendMessageCommandTests
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly IContextWindowService _contextWindowService;
    private readonly IConversationContextService _conversationRagService;
    private readonly MimirMapper _mapper;
    private readonly IMessageBus _messageBus;
    private readonly SendMessageCommandHandler _handler;

    public SendMessageCommandTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _llmService = Substitute.For<ILlmService>();
        _contextWindowService = Substitute.For<IContextWindowService>();
        _conversationRagService = Substitute.For<IConversationContextService>();
        _messageBus = Substitute.For<IMessageBus>();

        _conversationRagService
            .GetRagContextAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<KnowledgeSearchResultDto>());

        // Default: BuildLlmMessagesAsync returns a basic message list
        _contextWindowService.BuildLlmMessagesAsync(
                Arg.Any<Conversation>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new List<LlmMessage>
            {
                new("system", "You are Mimir, a helpful AI assistant."),
                new("user", callInfo.ArgAt<string>(1)),
            });

        // Default: EstimateTokenCount delegates to the standard formula
        _contextWindowService.EstimateTokenCount(Arg.Any<string>())
            .Returns(callInfo =>
            {
                var text = callInfo.Arg<string>();
                return string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
            });

        _mapper = new MimirMapper();

        _handler = new SendMessageCommandHandler(
            _repository, _currentUserService, _unitOfWork, _llmService, _contextWindowService, _conversationRagService, _mapper, _messageBus);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnAssistantMessageDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var conversation = Conversation.Create(userId, "Test Chat");
        SetConversationId(conversation, conversationId);

        _repository.GetWithMessagesAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var chunks = CreateStreamChunks("Hello", " world", "!");
        _llmService.StreamMessageAsync("phi-4-mini", Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(chunks);

        var command = new SendMessageCommand(conversationId, "Hi there", null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Content.ShouldBe("Hello world!");
        result.Role.ShouldBe("Assistant");
        result.Model.ShouldBe("phi-4-mini");
        result.TokenCount.ShouldNotBeNull();
        result.ConversationId.ShouldBe(conversationId);

        await _repository.Received(1).UpdateAsync(conversation, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSpecificModel_ShouldUseRequestedModel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var conversation = Conversation.Create(userId, "Test Chat");
        SetConversationId(conversation, conversationId);

        _repository.GetWithMessagesAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var chunks = CreateStreamChunks("Response");
        _llmService.StreamMessageAsync("qwen-2.5-72b", Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(chunks);

        var command = new SendMessageCommand(conversationId, "Hello", "qwen-2.5-72b");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Model.ShouldBe("qwen-2.5-72b");
        _llmService.Received(1).StreamMessageAsync(
            "qwen-2.5-72b", Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DefaultsToPhiMini_WhenNoModelSpecified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var conversation = Conversation.Create(userId, "Test Chat");
        SetConversationId(conversation, conversationId);

        _repository.GetWithMessagesAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var chunks = CreateStreamChunks("OK");
        _llmService.StreamMessageAsync("phi-4-mini", Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(chunks);

        var command = new SendMessageCommand(conversationId, "Hello", null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _llmService.Received(1).StreamMessageAsync(
            "phi-4-mini", Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotAuthenticated_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);
        var command = new SendMessageCommand(Guid.NewGuid(), "Hello", null);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ConversationNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        _repository.GetWithMessagesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        var command = new SendMessageCommand(Guid.NewGuid(), "Hello", null);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ConversationBelongsToDifferentUser_ShouldThrowNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var conversation = Conversation.Create(otherUserId, "Other User Chat");

        _repository.GetWithMessagesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        var command = new SendMessageCommand(conversation.Id, "Hello", null);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldPersistBothUserAndAssistantMessages()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var conversation = Conversation.Create(userId, "Test Chat");
        SetConversationId(conversation, conversationId);

        _repository.GetWithMessagesAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var chunks = CreateStreamChunks("Assistant reply");
        _llmService.StreamMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(chunks);

        var command = new SendMessageCommand(conversationId, "User question", null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — conversation should have both user and assistant messages
        conversation.Messages.Count.ShouldBe(2);
        conversation.Messages.ShouldContain(m => m.Role == MessageRole.User && m.Content == "User question");
        conversation.Messages.ShouldContain(m => m.Role == MessageRole.Assistant && m.Content == "Assistant reply");
    }

    [Fact]
    public async Task Handle_ShouldSetTokenCountOnAssistantMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var conversation = Conversation.Create(userId, "Test Chat");
        SetConversationId(conversation, conversationId);

        _repository.GetWithMessagesAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var responseText = "This is a test response";
        var chunks = CreateStreamChunks(responseText);
        _llmService.StreamMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(chunks);

        var command = new SendMessageCommand(conversationId, "Hello", null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var expectedTokens = (responseText.Length + 3) / 4;
        result.TokenCount.ShouldBe(expectedTokens);
    }

    [Fact]
    public async Task Handle_WhenOriginLinksExist_AppendsSourcesSectionToAssistantMessage()
    {
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var conversation = Conversation.Create(userId, "Test Chat");
        SetConversationId(conversation, conversationId);

        _repository.GetWithMessagesAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        _conversationRagService
            .GetRagContextAsync(conversationId, "Where is the auth flow?", Arg.Any<CancellationToken>())
            .Returns(
            [
                new KnowledgeSearchResultDto(
                    Guid.NewGuid(),
                    "auth chunk",
                    0.98f,
                    "Code",
                    "auth-service",
                    new SourceOriginLinkDto("https://mediahub/files/auth-service", "Code", "AuthService.cs", 45, 78)),
            ]);

        _llmService.StreamMessageAsync("phi-4-mini", Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(CreateStreamChunks("Answer body"));

        var result = await _handler.Handle(
            new SendMessageCommand(conversationId, "Where is the auth flow?", null),
            TestContext.Current.CancellationToken);

        result.Content.Should().Be("Answer body\n\n**Sources:**\n- [💻 AuthService.cs:45-78](https://mediahub/files/auth-service)");
        conversation.Messages.Should().ContainSingle(message =>
            message.Role == MessageRole.Assistant &&
            message.Content == result.Content);
    }

    // Context window algorithm tests (BuildLlmMessages, EstimateTokenCount, GetTokenLimit)
    // have been moved to nem.Mimir.Infrastructure.Tests/Services/ContextWindowServiceTests.cs
    // since the logic now lives in ContextWindowService.

    private static void SetConversationId(Conversation conversation, Guid id)
    {
        // Use reflection to set the Id for testing purposes
        var prop = typeof(Conversation).BaseType!.BaseType!.GetProperty("Id");
        prop!.SetValue(conversation, id);
    }

    private static async IAsyncEnumerable<LlmStreamChunk> CreateStreamChunks(params string[] contents)
    {
        foreach (var content in contents)
        {
            yield return new LlmStreamChunk(content, "phi-4-mini", null);
        }

        await Task.CompletedTask;
    }
}
