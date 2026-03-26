using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.McpServers.Queries;
using nem.Mimir.Domain.McpServers;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.McpServers;

public sealed class ReadMcpResourceQueryHandlerTests
{
    private readonly IMcpServerConfigRepository _repository = Substitute.For<IMcpServerConfigRepository>();
    private readonly IMcpClientManager _clientManager = Substitute.For<IMcpClientManager>();

    private ReadMcpResourceQueryHandler CreateHandler() => new(_repository, _clientManager);

    [Fact]
    public async Task Handle_ShouldReturnResourceContents()
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

        var contents = new List<McpResourceContent>
        {
            new("file:///readme.md", "text/markdown", "# Hello World", null),
        };

        _clientManager.ReadResourceAsync(serverId, "file:///readme.md", Arg.Any<CancellationToken>())
            .Returns(contents);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new ReadMcpResourceQuery(serverId, "file:///readme.md"), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Uri.ShouldBe("file:///readme.md");
        result[0].MimeType.ShouldBe("text/markdown");
        result[0].Text.ShouldBe("# Hello World");
        result[0].Blob.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_WithBinaryResource_ShouldReturnBlobContent()
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

        var base64Data = Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47]);
        var contents = new List<McpResourceContent>
        {
            new("file:///image.png", "image/png", null, base64Data),
        };

        _clientManager.ReadResourceAsync(serverId, "file:///image.png", Arg.Any<CancellationToken>())
            .Returns(contents);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new ReadMcpResourceQuery(serverId, "file:///image.png"), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Text.ShouldBeNull();
        result[0].Blob.ShouldBe(base64Data);
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
                new ReadMcpResourceQuery(serverId, "file:///readme.md"), CancellationToken.None));
    }
}
