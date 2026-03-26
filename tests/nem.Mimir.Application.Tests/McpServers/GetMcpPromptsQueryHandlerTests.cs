using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.McpServers.Queries;
using nem.Mimir.Domain.McpServers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace nem.Mimir.Application.Tests.McpServers;

public sealed class GetMcpPromptsQueryHandlerTests
{
    private readonly IMcpServerConfigRepository _repository = Substitute.For<IMcpServerConfigRepository>();
    private readonly IMcpClientManager _clientManager = Substitute.For<IMcpClientManager>();
    private readonly ILogger<GetMcpPromptsQueryHandler> _logger = Substitute.For<ILogger<GetMcpPromptsQueryHandler>>();

    private GetMcpPromptsQueryHandler CreateHandler() => new(_repository, _clientManager, _logger);

    [Fact]
    public async Task Handle_ShouldReturnPromptsWithArguments()
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

        var prompts = new List<McpPromptDefinition>
        {
            new("code-review", "Reviews code for issues", new List<McpPromptArgument>
            {
                new("language", "Programming language", true),
                new("style", "Review style", false),
            }),
            new("summarize", "Summarizes text", null),
        };

        _clientManager.ListPromptsAsync(serverId, Arg.Any<CancellationToken>())
            .Returns(prompts);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetMcpPromptsQuery(serverId), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(2);

        result[0].Name.ShouldBe("code-review");
        result[0].Description.ShouldBe("Reviews code for issues");
        result[0].Arguments.ShouldNotBeNull();
        result[0].Arguments!.Count.ShouldBe(2);
        result[0].Arguments![0].Name.ShouldBe("language");
        result[0].Arguments![0].Required.ShouldBeTrue();
        result[0].Arguments![1].Name.ShouldBe("style");
        result[0].Arguments![1].Required.ShouldBeFalse();

        result[1].Name.ShouldBe("summarize");
        result[1].Arguments.ShouldBeNull();
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
            () => handler.Handle(new GetMcpPromptsQuery(serverId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenClientManagerThrows_ShouldReturnEmptyList()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var config = new McpServerConfig
        {
            Id = serverId,
            Name = "disconnected-server",
            TransportType = McpTransportType.Sse,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _repository.GetByIdAsync(serverId, Arg.Any<CancellationToken>())
            .Returns(config);

        _clientManager.ListPromptsAsync(serverId, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Server not connected"));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetMcpPromptsQuery(serverId), CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }
}
