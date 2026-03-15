using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.McpServers.Commands;
using nem.Mimir.Domain.McpServers;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.McpServers;

public sealed class ToggleMcpServerHandlerTests
{
    private readonly IMcpServerConfigRepository _repository = Substitute.For<IMcpServerConfigRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private ToggleMcpServerCommandHandler CreateHandler() => new(_repository, _unitOfWork);

    [Fact]
    public async Task Handle_ShouldEnableServer()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var config = new McpServerConfig
        {
            Id = serverId,
            Name = "toggle-test",
            TransportType = McpTransportType.Stdio,
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _repository.GetByIdAsync(serverId, Arg.Any<CancellationToken>())
            .Returns(config);

        var handler = CreateHandler();

        // Act
        await handler.Handle(new ToggleMcpServerCommand(serverId, true), CancellationToken.None);

        // Assert
        config.IsEnabled.ShouldBeTrue();
        await _repository.Received(1).UpdateAsync(config, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDisableServer()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var config = new McpServerConfig
        {
            Id = serverId,
            Name = "toggle-test",
            TransportType = McpTransportType.Sse,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _repository.GetByIdAsync(serverId, Arg.Any<CancellationToken>())
            .Returns(config);

        var handler = CreateHandler();

        // Act
        await handler.Handle(new ToggleMcpServerCommand(serverId, false), CancellationToken.None);

        // Assert
        config.IsEnabled.ShouldBeFalse();
        await _repository.Received(1).UpdateAsync(config, Arg.Any<CancellationToken>());
    }
}
