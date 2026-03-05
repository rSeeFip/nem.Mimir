using Mimir.Application.McpServers.Queries;
using Mimir.Domain.McpServers;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.McpServers;

public sealed class GetMcpServersHandlerTests
{
    private readonly IMcpServerConfigRepository _repository = Substitute.For<IMcpServerConfigRepository>();

    private GetMcpServersQueryHandler CreateHandler() => new(_repository);

    [Fact]
    public async Task Handle_ShouldReturnAllServers()
    {
        // Arrange
        var configs = new List<McpServerConfig>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "server-1",
                TransportType = McpTransportType.Stdio,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "server-2",
                TransportType = McpTransportType.Sse,
                IsEnabled = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(configs);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetMcpServersQuery(), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("server-1");
        result[0].TransportType.ShouldBe("Stdio");
        result[1].Name.ShouldBe("server-2");
        result[1].TransportType.ShouldBe("Sse");
    }

    [Fact]
    public async Task Handle_ShouldMapWhitelistsCorrectly()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var config = new McpServerConfig
        {
            Id = serverId,
            Name = "server-with-whitelist",
            TransportType = McpTransportType.Stdio,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ToolWhitelists = new List<McpToolWhitelist>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    McpServerConfigId = serverId,
                    ToolName = "read_file",
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow,
                },
            },
        };

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig> { config });

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetMcpServersQuery(), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
        result[0].ToolWhitelists.Count.ShouldBe(1);
        result[0].ToolWhitelists[0].ToolName.ShouldBe("read_file");
        result[0].ToolWhitelists[0].IsEnabled.ShouldBeTrue();
    }
}
