using Mimir.Application.Common.Interfaces;
using Mimir.Application.McpServers.Commands;
using Mimir.Domain.McpServers;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.McpServers;

public sealed class CreateMcpServerHandlerTests
{
    private readonly IMcpServerConfigRepository _repository = Substitute.For<IMcpServerConfigRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private CreateMcpServerCommandHandler CreateHandler() => new(_repository, _unitOfWork);

    [Fact]
    public async Task Handle_ShouldCreateServerAndReturnId()
    {
        // Arrange
        var command = new CreateMcpServerCommand(
            Name: "test-server",
            TransportType: McpTransportType.Stdio,
            Description: "A test server",
            Command: "/usr/bin/test",
            Arguments: "--flag",
            Url: null,
            EnvironmentVariablesJson: null,
            IsEnabled: true);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBe(Guid.Empty);
        await _repository.Received(1).AddAsync(
            Arg.Is<McpServerConfig>(c =>
                c.Name == "test-server" &&
                c.TransportType == McpTransportType.Stdio &&
                c.IsEnabled == true &&
                c.IsBundled == false),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSetDefaultValuesCorrectly()
    {
        // Arrange
        var command = new CreateMcpServerCommand(
            Name: "default-server",
            TransportType: McpTransportType.Sse,
            Description: null,
            Command: null,
            Arguments: null,
            Url: "http://localhost:8080",
            EnvironmentVariablesJson: null);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBe(Guid.Empty);
        await _repository.Received(1).AddAsync(
            Arg.Is<McpServerConfig>(c =>
                c.IsEnabled == false &&
                c.IsBundled == false &&
                c.Description == null &&
                c.Url == "http://localhost:8080"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSetTimestamps()
    {
        // Arrange
        var before = DateTime.UtcNow;
        var command = new CreateMcpServerCommand(
            Name: "timestamp-test",
            TransportType: McpTransportType.StreamableHttp,
            Description: null,
            Command: null,
            Arguments: null,
            Url: "http://localhost",
            EnvironmentVariablesJson: null);

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);
        var after = DateTime.UtcNow;

        // Assert
        await _repository.Received(1).AddAsync(
            Arg.Is<McpServerConfig>(c =>
                c.CreatedAt >= before && c.CreatedAt <= after &&
                c.UpdatedAt >= before && c.UpdatedAt <= after),
            Arg.Any<CancellationToken>());
    }
}
