using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Application.Services.Memory;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;
using nem.Contracts.Memory;

namespace Mimir.Application.Tests.Memory;

public sealed class WorkingMemoryServiceTests
{
    private readonly IConversationRepository _repository = Substitute.For<IConversationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILlmService _llmService = Substitute.For<ILlmService>();
    private readonly ILogger<PersistentWorkingMemoryService> _logger = Substitute.For<ILogger<PersistentWorkingMemoryService>>();

    [Fact]
    public async Task GetWindowAsync_ReturnsMessagesWithinTokenBudget()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "window");
        var first = AddMessage(conversation, MessageRole.User, "m1", 100, DateTimeOffset.UtcNow.AddMinutes(-3));
        var second = AddMessage(conversation, MessageRole.Assistant, "m2", 110, DateTimeOffset.UtcNow.AddMinutes(-2));
        var third = AddMessage(conversation, MessageRole.User, "m3", 120, DateTimeOffset.UtcNow.AddMinutes(-1));

        SetupConversation(conversation);
        var sut = CreateSut();

        var window = await sut.GetWindowAsync(conversation.Id.ToString(), 230, CancellationToken.None);

        window.Count.ShouldBe(2);
        window[0].Content.ShouldBe(second.Content);
        window[1].Content.ShouldBe(third.Content);
        SumTokens(window).ShouldBeLessThanOrEqualTo(230);
        first.Content.ShouldNotBe(window[0].Content);
    }

    [Fact]
    public async Task GetWindowAsync_StopsAtTokenLimit()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "window-limit");
        AddMessage(conversation, MessageRole.User, "a", 80, DateTimeOffset.UtcNow.AddMinutes(-3));
        AddMessage(conversation, MessageRole.Assistant, "b", 90, DateTimeOffset.UtcNow.AddMinutes(-2));
        AddMessage(conversation, MessageRole.User, "c", 100, DateTimeOffset.UtcNow.AddMinutes(-1));

        SetupConversation(conversation);
        var sut = CreateSut();

        var window = await sut.GetWindowAsync(conversation.Id.ToString(), 170, CancellationToken.None);

        window.Count.ShouldBe(1);
        window[0].Content.ShouldBe("c");
        SumTokens(window).ShouldBeLessThanOrEqualTo(170);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsLastNMessages()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "recent");
        AddMessage(conversation, MessageRole.User, "one", 10, DateTimeOffset.UtcNow.AddMinutes(-3));
        AddMessage(conversation, MessageRole.Assistant, "two", 10, DateTimeOffset.UtcNow.AddMinutes(-2));
        AddMessage(conversation, MessageRole.User, "three", 10, DateTimeOffset.UtcNow.AddMinutes(-1));

        SetupConversation(conversation);
        var sut = CreateSut();

        var recent = await sut.GetRecentAsync(conversation.Id.ToString(), 2, CancellationToken.None);

        recent.Count.ShouldBe(2);
        recent[0].Content.ShouldBe("three");
        recent[1].Content.ShouldBe("two");
    }

    [Fact]
    public async Task GetTokenCountAsync_SumsTokenCounts()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "tokens");
        AddMessage(conversation, MessageRole.User, "one", 15, DateTimeOffset.UtcNow.AddMinutes(-3));
        AddMessage(conversation, MessageRole.Assistant, "two", 25, DateTimeOffset.UtcNow.AddMinutes(-2));
        AddMessage(conversation, MessageRole.User, "three", 35, DateTimeOffset.UtcNow.AddMinutes(-1));

        SetupConversation(conversation);
        var sut = CreateSut();

        var total = await sut.GetTokenCountAsync(conversation.Id.ToString(), CancellationToken.None);

        total.ShouldBe(75);
    }

    [Fact]
    public async Task AddMessageAsync_TriggersSummarization_WhenBudgetExceeded()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "summary");
        AddMessage(conversation, MessageRole.User, "older one", 30, DateTimeOffset.UtcNow.AddMinutes(-3));
        AddMessage(conversation, MessageRole.Assistant, "older two", 40, DateTimeOffset.UtcNow.AddMinutes(-2));

        SetupConversation(conversation);
        _llmService.SendMessageAsync(
                "summary-model",
                Arg.Any<IReadOnlyList<LlmMessage>>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("compressed summary", "summary-model", 0, 0, 0, "stop"));

        var sut = CreateSut(new WorkingMemoryOptions
        {
            MaxTokenWindow = 100,
            SummarizationThreshold = 75,
            SummarizationModel = "summary-model"
        });

        await sut.AddMessageAsync(
            conversation.Id.ToString(),
            new ConversationMessage(
                "user",
                "new message",
                DateTimeOffset.UtcNow,
                new Dictionary<string, string> { ["tokenCount"] = "40" }),
            CancellationToken.None);

        await _llmService.Received(1).SendMessageAsync(
            "summary-model",
            Arg.Any<IReadOnlyList<LlmMessage>>(),
            Arg.Any<CancellationToken>());

        conversation.Messages.ShouldContain(m =>
            m.Role == MessageRole.System &&
            m.Content.StartsWith("[working-memory-summary]", StringComparison.OrdinalIgnoreCase));
    }

    private PersistentWorkingMemoryService CreateSut(WorkingMemoryOptions? options = null)
    {
        var value = options ?? new WorkingMemoryOptions();
        return new PersistentWorkingMemoryService(_repository, _unitOfWork, _llmService, value, _logger);
    }

    private void SetupConversation(Conversation conversation)
    {
        _repository.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);
    }

    private static Message AddMessage(Conversation conversation, MessageRole role, string content, int tokens, DateTimeOffset createdAt)
    {
        var message = conversation.AddMessage(role, content);
        message.SetTokenCount(tokens);

        var createdAtProperty = typeof(Message).GetProperty(nameof(Message.CreatedAt));
        createdAtProperty!.SetValue(message, createdAt);
        return message;
    }

    private static int SumTokens(IReadOnlyList<ConversationMessage> messages)
    {
        return messages.Sum(m =>
        {
            if (m.Metadata is not null &&
                m.Metadata.TryGetValue("tokenCount", out var raw) &&
                int.TryParse(raw, out var parsed))
            {
                return parsed;
            }

            return 0;
        });
    }
}
