using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.McpServers.Commands;
using nem.Mimir.Domain.McpServers;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.McpServers;

public sealed class UpdateToolWhitelistHandlerTests
{
    private readonly IMcpServerConfigRepository _repository = Substitute.For<IMcpServerConfigRepository>();
    private readonly IToolWhitelistService _whitelistService = Substitute.For<IToolWhitelistService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private UpdateToolWhitelistCommandHandler CreateHandler() => new(_repository, _whitelistService, _unitOfWork);

    [Fact]
    public async Task Handle_ShouldSetWhitelistEntries()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var config = new McpServerConfig
        {
            Id = serverId,
            Name = "whitelist-test",
            TransportType = McpTransportType.Stdio,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _repository.GetByIdAsync(serverId, Arg.Any<CancellationToken>())
            .Returns(config);

        var entries = new List<ToolWhitelistEntry>
        {
            new("read_file", true),
            new("write_file", false),
        };

        var handler = CreateHandler();

        // Act
        await handler.Handle(new UpdateToolWhitelistCommand(serverId, entries), CancellationToken.None);

        // Assert
        await _whitelistService.Received(1).SetToolWhitelistAsync(
            serverId, "read_file", true, Arg.Any<CancellationToken>());
        await _whitelistService.Received(1).SetToolWhitelistAsync(
            serverId, "write_file", false, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenServerNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        _repository.GetByIdAsync(serverId, Arg.Any<CancellationToken>())
            .Returns((McpServerConfig?)null);

        var handler = CreateHandler();
        var entries = new List<ToolWhitelistEntry> { new("read_file", true) };

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(new UpdateToolWhitelistCommand(serverId, entries), CancellationToken.None));
    }
}
