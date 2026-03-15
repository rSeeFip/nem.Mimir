using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.McpServers;

namespace nem.Mimir.Application.McpServers.Commands;

/// <summary>
/// Command to delete an MCP server configuration.
/// </summary>
public sealed record DeleteMcpServerCommand(Guid Id) : ICommand;

internal sealed class DeleteMcpServerCommandHandler(
    IMcpServerConfigRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteMcpServerCommand>
{
    public async Task Handle(DeleteMcpServerCommand request, CancellationToken cancellationToken)
    {
        var config = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(McpServerConfig), request.Id);

        await repository.DeleteAsync(config.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
