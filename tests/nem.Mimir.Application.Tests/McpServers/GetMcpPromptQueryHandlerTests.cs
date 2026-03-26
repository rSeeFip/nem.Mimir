using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.McpServers.Queries;
using nem.Mimir.Domain.McpServers;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.McpServers;

public sealed class GetMcpPromptQueryHandlerTests
{
    private readonly IMcpServerConfigRepository _repository = Substitute.For<IMcpServerConfigRepository>();
    private readonly IMcpClientManager _clientManager = Substitute.For<IMcpClientManager>();

    private GetMcpPromptQueryHandler CreateHandler() => new(_repository, _clientManager);

    [Fact]
    public async Task Handle_ShouldReturnPromptResult()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var config = new McpServerConfig
        {
            Id = serverId,
            Name = "test-server",
            TransportType = McpTransportType.Stdio,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _repository.GetByIdAsync(serverId, Arg.Any<CancellationToken>())
            .Returns(config);

        var promptResult = new McpPromptResult("Code review prompt", new List<McpPromptMessage>
        {
            new("user", "Please review the following code:"),
            new("assistant", "I'll review your code for best practices."),
        });

        var arguments = new Dictionary<string, string> { ["language"] = "csharp" };

        _clientManager.GetPromptAsync(serverId, "code-review", Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(promptResult);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new GetMcpPromptQuery(serverId, "code-review", arguments), CancellationToken.None);

        // Assert
        result.Description.ShouldBe("Code review prompt");
        result.Messages.Count.ShouldBe(2);
        result.Messages[0].Role.ShouldBe("user");
        result.Messages[0].Content.ShouldBe("Please review the following code:");
        result.Messages[1].Role.ShouldBe("assistant");
    }

    [Fact]
    public async Task Handle_WithoutArguments_ShouldWork()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var config = new McpServerConfig
        {
            Id = serverId,
            Name = "test-server",
            TransportType = McpTransportType.Stdio,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _repository.GetByIdAsync(serverId, Arg.Any<CancellationToken>())
            .Returns(config);

        var promptResult = new McpPromptResult(null, new List<McpPromptMessage>
        {
            new("user", "Summarize the following:"),
        });

        _clientManager.GetPromptAsync(serverId, "summarize", Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(promptResult);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new GetMcpPromptQuery(serverId, "summarize"), CancellationToken.None);

        // Assert
        result.Description.ShouldBeNull();
        result.Messages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_WhenServerNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        _repository.GetByIdAsync(serverId, Arg.Any<CancellationToken>())
            .Returns((McpServerConfig?)null);

        var handler = CreateHandler();

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(
                new GetMcpPromptQuery(serverId, "code-review"), CancellationToken.None));
    }
}
