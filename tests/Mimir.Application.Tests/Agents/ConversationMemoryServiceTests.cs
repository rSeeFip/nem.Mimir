using Microsoft.Extensions.Logging;
using Mimir.Application.Agents;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Agents;

public sealed class ConversationMemoryServiceTests
{
    private readonly ConversationMemoryService _service;

    public ConversationMemoryServiceTests()
    {
        var logger = Substitute.For<ILogger<ConversationMemoryService>>();
        _service = new ConversationMemoryService(logger, contextWindow: 5);
    }

    [Fact]
    public void GetOrCreateSession_NewConversation_CreatesSession()
    {
        var session = _service.GetOrCreateSession("conv-1", "user-1");

        session.ShouldNotBeNull();
        session.UserId.ShouldBe("user-1");
        session.Messages.ShouldBeEmpty();
        _service.SessionExists("conv-1").ShouldBeTrue();
    }

    [Fact]
    public void GetOrCreateSession_ExistingConversation_ReturnsSameSession()
    {
        var first = _service.GetOrCreateSession("conv-1");
        var second = _service.GetOrCreateSession("conv-1");

        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void GetOrCreateSession_EmptyId_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => _service.GetOrCreateSession(""));
    }

    [Fact]
    public void AddUserMessage_StoresMessageInSession()
    {
        _service.AddUserMessage("conv-1", "Hello", "user-1");

        _service.GetMessageCount("conv-1").ShouldBe(1);

        var history = _service.GetConversationHistory("conv-1");
        history.Count.ShouldBe(1);
        history[0].Role.ShouldBe("user");
        history[0].Content.ShouldBe("Hello");
    }

    [Fact]
    public void AddAssistantMessage_StoresMessageInSession()
    {
        _service.AddUserMessage("conv-1", "Hello");
        _service.AddAssistantMessage("conv-1", "Hi there!");

        _service.GetMessageCount("conv-1").ShouldBe(2);

        var history = _service.GetConversationHistory("conv-1");
        history.Count.ShouldBe(2);
        history[1].Role.ShouldBe("assistant");
        history[1].Content.ShouldBe("Hi there!");
    }

    [Fact]
    public void GetConversationHistory_RespectsContextWindow()
    {
        // Context window is 5, add 8 messages
        for (var i = 0; i < 8; i++)
        {
            _service.AddUserMessage("conv-1", $"Message {i}");
        }

        var history = _service.GetConversationHistory("conv-1");

        history.Count.ShouldBe(5);
        // Should contain the last 5 messages (index 3-7)
        history[0].Content.ShouldBe("Message 3");
        history[4].Content.ShouldBe("Message 7");
    }

    [Fact]
    public void GetConversationHistory_NonexistentSession_ReturnsEmpty()
    {
        var history = _service.GetConversationHistory("nonexistent");

        history.ShouldBeEmpty();
    }

    [Fact]
    public void GetMessageCount_NonexistentSession_ReturnsZero()
    {
        _service.GetMessageCount("nonexistent").ShouldBe(0);
    }

    [Fact]
    public void SessionExists_NonexistentSession_ReturnsFalse()
    {
        _service.SessionExists("nonexistent").ShouldBeFalse();
    }

    [Fact]
    public void ContextWindow_ReturnsConfiguredValue()
    {
        _service.ContextWindow.ShouldBe(5);
    }
}
