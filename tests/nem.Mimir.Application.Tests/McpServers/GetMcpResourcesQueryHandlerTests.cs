using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.McpServers.Queries;
using nem.Mimir.Domain.McpServers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace nem.Mimir.Application.Tests.McpServers;

public sealed class GetMcpResourcesQueryHandlerTests
{
    private readonly IMcpServerConfigRepository _repository = Substitute.For<IMcpServerConfigRepository>();
    private readonly IMcpClientManager _clientManager = Substitute.For<IMcpClientManager>();
    private readonly ILogger<GetMcpResourcesQueryHandler> _logger = Substitute.For<ILogger<GetMcpResourcesQueryHandler>>();

    private GetMcpResourcesQueryHandler CreateHandler() => new(_repository, _clientManager, _logger);

    [Fact]
    public async Task Handle_ShouldReturnResourcesAndTemplates()
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

        var resources = new List<McpResourceDefinition>
        {
            new("file:///readme.md", "README", "Project readme", "text/markdown"),
            new("file:///config.json", "Config", null, "application/json"),
        };

        var templates = new List<McpResourceTemplateDefinition>
        {
            new("file:///logs/{date}.log", "Daily Log", "Log file by date", "text/plain"),
        };

        _clientManager.ListResourcesAsync(serverId, Arg.Any<CancellationToken>())
            .Returns(resources);
        _clientManager.ListResourceTemplatesAsync(serverId, Arg.Any<CancellationToken>())
            .Returns(templates);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetMcpResourcesQuery(serverId), CancellationToken.None);

        // Assert
        result.Resources.Count.ShouldBe(2);
        result.Resources[0].Uri.ShouldBe("file:///readme.md");
        result.Resources[0].Name.ShouldBe("README");
        result.Resources[0].Description.ShouldBe("Project readme");
        result.Resources[0].MimeType.ShouldBe("text/markdown");
        result.Resources[1].Uri.ShouldBe("file:///config.json");
        result.Resources[1].Description.ShouldBeNull();

        result.Templates.Count.ShouldBe(1);
        result.Templates[0].UriTemplate.ShouldBe("file:///logs/{date}.log");
        result.Templates[0].Name.ShouldBe("Daily Log");
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
            () => handler.Handle(new GetMcpResourcesQuery(serverId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenClientManagerThrows_ShouldReturnEmptyResult()
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

        _clientManager.ListResourcesAsync(serverId, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Server not connected"));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetMcpResourcesQuery(serverId), CancellationToken.None);

        // Assert
        result.Resources.ShouldBeEmpty();
        result.Templates.ShouldBeEmpty();
    }
}
