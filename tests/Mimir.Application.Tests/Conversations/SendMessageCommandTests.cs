using AutoMapper;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Application.Conversations.Commands;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Conversations;

public sealed class SendMessageCommandTests
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly IMapper _mapper;
    private readonly SendMessageCommandHandler _handler;

    public SendMessageCommandTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _llmService = Substitute.For<ILlmService>();

        var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = mapperConfig.CreateMapper();

        _handler = new SendMessageCommandHandler(
            _repository, _currentUserService, _unitOfWork, _llmService, _mapper);
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
    public void BuildLlmMessages_ShouldIncludeSystemPromptAndNewUserMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Test");

        // Act
        var messages = SendMessageCommandHandler.BuildLlmMessages(conversation, "Hello", 16_384);

        // Assert
        messages.Count.ShouldBe(2);
        messages[0].Role.ShouldBe("system");
        messages[0].Content.ShouldBe("You are Mimir, a helpful AI assistant.");
        messages[1].Role.ShouldBe("user");
        messages[1].Content.ShouldBe("Hello");
    }

    [Fact]
    public void BuildLlmMessages_ShouldIncludeHistoryMessages()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Test");
        conversation.AddMessage(MessageRole.User, "First question");
        conversation.AddMessage(MessageRole.Assistant, "First answer");

        // Act
        var messages = SendMessageCommandHandler.BuildLlmMessages(conversation, "Second question", 131_072);

        // Assert
        messages.Count.ShouldBe(4); // system + 2 history + new user message
        messages[0].Role.ShouldBe("system");
        messages[1].Role.ShouldBe("user");
        messages[1].Content.ShouldBe("First question");
        messages[2].Role.ShouldBe("assistant");
        messages[2].Content.ShouldBe("First answer");
    }

    [Fact]
    public void BuildLlmMessages_ShouldTruncateOldestMessagesWhenExceedingLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Test");

        // Add many large messages to exceed a small token limit
        for (var i = 0; i < 10; i++)
        {
            conversation.AddMessage(MessageRole.User, new string('x', 400)); // ~100 tokens each
            conversation.AddMessage(MessageRole.Assistant, new string('y', 400));
        }

        // Act — use very small token limit to force truncation
        var messages = SendMessageCommandHandler.BuildLlmMessages(conversation, "New question", 300);

        // Assert
        messages.Count.ShouldBeGreaterThan(1); // at least system + new user
        messages[0].Role.ShouldBe("system"); // system prompt always preserved
        messages[^1].Role.ShouldBe("user"); // new user message always at end
        messages[^1].Content.ShouldBe("New question");

        // Total messages should be fewer than all history + system + new user
        messages.Count.ShouldBeLessThan(22); // 20 history + system + new user
    }

    [Fact]
    public void BuildLlmMessages_ShouldAlwaysPreserveSystemPromptAndNewMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Test");

        // Add messages that alone exceed a tiny limit
        conversation.AddMessage(MessageRole.User, new string('a', 1000));
        conversation.AddMessage(MessageRole.Assistant, new string('b', 1000));

        // Act — very tight limit, should drop all history but keep system + new user
        var messages = SendMessageCommandHandler.BuildLlmMessages(conversation, "Hi", 50);

        // Assert
        messages[0].Role.ShouldBe("system");
        messages[0].Content.ShouldBe("You are Mimir, a helpful AI assistant.");
        messages[^1].Role.ShouldBe("user");
        messages[^1].Content.ShouldBe("Hi");
    }

    [Fact]
    public void BuildLlmMessages_HistoryOrderedByCreatedAt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Test");
        conversation.AddMessage(MessageRole.User, "First");
        conversation.AddMessage(MessageRole.Assistant, "Second");
        conversation.AddMessage(MessageRole.User, "Third");

        // Act
        var messages = SendMessageCommandHandler.BuildLlmMessages(conversation, "Fourth", 131_072);

        // Assert — history should be in chronological order
        var historyMessages = messages.Skip(1).SkipLast(1).ToList();
        historyMessages[0].Content.ShouldBe("First");
        historyMessages[1].Content.ShouldBe("Second");
        historyMessages[2].Content.ShouldBe("Third");
    }

    [Fact]
    public void Validator_EmptyConversationId_ShouldFail()
    {
        // Arrange
        var validator = new SendMessageCommandValidator();
        var command = new SendMessageCommand(Guid.Empty, "Hello", null);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ConversationId");
    }

    [Fact]
    public void Validator_EmptyContent_ShouldFail()
    {
        // Arrange
        var validator = new SendMessageCommandValidator();
        var command = new SendMessageCommand(Guid.NewGuid(), "", null);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Content");
    }

    [Fact]
    public void Validator_ContentTooLong_ShouldFail()
    {
        // Arrange
        var validator = new SendMessageCommandValidator();
        var command = new SendMessageCommand(Guid.NewGuid(), new string('x', 32_001), null);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Content");
    }

    [Fact]
    public void Validator_ValidCommand_ShouldPass()
    {
        // Arrange
        var validator = new SendMessageCommandValidator();
        var command = new SendMessageCommand(Guid.NewGuid(), "Hello there!", null);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validator_ValidCommandWithModel_ShouldPass()
    {
        // Arrange
        var validator = new SendMessageCommandValidator();
        var command = new SendMessageCommand(Guid.NewGuid(), "Hello", "qwen-2.5-72b");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EstimateTokenCount_ShouldMatchLlmModelsFormula()
    {
        // Verify our estimate matches the infrastructure formula: (text.Length + 3) / 4
        SendMessageCommandHandler.EstimateTokenCount("").ShouldBe(0);
        SendMessageCommandHandler.EstimateTokenCount("a").ShouldBe(1);
        SendMessageCommandHandler.EstimateTokenCount("abcd").ShouldBe(1); // (4+3)/4 = 1
        SendMessageCommandHandler.EstimateTokenCount("abcde").ShouldBe(2); // (5+3)/4 = 2
        SendMessageCommandHandler.EstimateTokenCount("Hello, world!").ShouldBe(4); // (13+3)/4 = 4
    }

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
