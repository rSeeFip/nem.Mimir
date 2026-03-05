using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.McpServers.Commands;
using Mimir.Domain.McpServers;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.McpServers;

public sealed class DeleteMcpServerHandlerTests
{
    private readonly IMcpServerConfigRepository _repository = Substitute.For<IMcpServerConfigRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private DeleteMcpServerCommandHandler CreateHandler() => new(_repository, _unitOfWork);

    [Fact]
    public async Task Handle_WhenServerExists_ShouldDelete()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var config = new McpServerConfig
        {
            Id = serverId,
            Name = "to-delete",
            TransportType = McpTransportType.Stdio,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _repository.GetByIdAsync(serverId, Arg.Any<CancellationToken>())
            .Returns(config);

        var handler = CreateHandler();

        // Act
        await handler.Handle(new DeleteMcpServerCommand(serverId), CancellationToken.None);

        // Assert
        await _repository.Received(1).DeleteAsync(serverId, Arg.Any<CancellationToken>());
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

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(new DeleteMcpServerCommand(serverId), CancellationToken.None));
    }
}
