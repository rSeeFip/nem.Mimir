using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.OpenAiCompat.Commands;
using nem.Mimir.Application.OpenAiCompat.Models;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.OpenAiCompat;

public sealed class CreateChatCompletionCommandTests
{
    private readonly ILlmService _llmService;
    private readonly CreateChatCompletionCommandHandler _handler;

    public CreateChatCompletionCommandTests()
    {
        _llmService = Substitute.For<ILlmService>();
        _handler = new CreateChatCompletionCommandHandler(_llmService);
    }

    [Fact]
    public async Task Handle_ReturnsCompletionResult()
    {
        // Arrange
        var llmResponse = new LlmResponse(
            Content: "Hello! How can I help you?",
            Model: "phi-4-mini",
            PromptTokens: 10,
            CompletionTokens: 8,
            TotalTokens: 18,
            FinishReason: "stop");

        _llmService.SendMessageAsync(
                "phi-4-mini",
                Arg.Any<IReadOnlyList<LlmMessage>>(),
                Arg.Any<CancellationToken>())
            .Returns(llmResponse);

        var command = new CreateChatCompletionCommand(
            "phi-4-mini",
            [new ChatMessageDto("user", "Hello")]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Content.ShouldBe("Hello! How can I help you?");
        result.Model.ShouldBe("phi-4-mini");
        result.FinishReason.ShouldBe("stop");
        result.PromptTokens.ShouldBe(10);
        result.CompletionTokens.ShouldBe(8);
        result.TotalTokens.ShouldBe(18);
        result.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task Handle_NullFinishReason_DefaultsToStop()
    {
        // Arrange
        var llmResponse = new LlmResponse(
            Content: "Done",
            Model: "qwen-2.5-72b",
            PromptTokens: 5,
            CompletionTokens: 1,
            TotalTokens: 6,
            FinishReason: null);

        _llmService.SendMessageAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<LlmMessage>>(),
                Arg.Any<CancellationToken>())
            .Returns(llmResponse);

        var command = new CreateChatCompletionCommand(
            "qwen-2.5-72b",
            [new ChatMessageDto("user", "Hi")]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.FinishReason.ShouldBe("stop");
    }

    [Fact]
    public async Task Handle_MapsMessagesToLlmMessages()
    {
        // Arrange
        var llmResponse = new LlmResponse("Reply", "model", 5, 2, 7, "stop");

        _llmService.SendMessageAsync(
                "model",
                Arg.Any<IReadOnlyList<LlmMessage>>(),
                Arg.Any<CancellationToken>())
            .Returns(llmResponse);

        var messages = new List<ChatMessageDto>
        {
            new("system", "You are helpful."),
            new("user", "What is 2+2?"),
        };

        var command = new CreateChatCompletionCommand("model", messages);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _llmService.Received(1).SendMessageAsync(
            "model",
            Arg.Is<IReadOnlyList<LlmMessage>>(msgs =>
                msgs.Count == 2 &&
                msgs[0].Role == "system" &&
                msgs[0].Content == "You are helpful." &&
                msgs[1].Role == "user" &&
                msgs[1].Content == "What is 2+2?"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MeasuresDuration()
    {
        // Arrange
        var llmResponse = new LlmResponse("Reply", "model", 5, 2, 7, "stop");

        _llmService.SendMessageAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<LlmMessage>>(),
                Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(50);
                return llmResponse;
            });

        var command = new CreateChatCompletionCommand(
            "model",
            [new ChatMessageDto("user", "Hi")]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Duration.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(30));
    }
}
