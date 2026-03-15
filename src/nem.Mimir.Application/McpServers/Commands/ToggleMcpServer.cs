using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.McpServers;

namespace nem.Mimir.Application.McpServers.Commands;

/// <summary>
/// Command to enable or disable an MCP server.
/// </summary>
public sealed record ToggleMcpServerCommand(
    Guid Id,
    bool IsEnabled) : ICommand;

internal sealed class ToggleMcpServerCommandHandler(
    IMcpServerConfigRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<ToggleMcpServerCommand>
{
    public async Task Handle(ToggleMcpServerCommand request, CancellationToken cancellationToken)
    {
        var config = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(McpServerConfig), request.Id);

        config.IsEnabled = request.IsEnabled;
        config.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(config, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
