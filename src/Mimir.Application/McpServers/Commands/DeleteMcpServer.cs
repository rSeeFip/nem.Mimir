using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.McpServers;

namespace Mimir.Application.McpServers.Commands;

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
