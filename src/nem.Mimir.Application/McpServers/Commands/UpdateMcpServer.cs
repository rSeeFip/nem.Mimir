using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.McpServers;

namespace nem.Mimir.Application.McpServers.Commands;

/// <summary>
/// Command to update an existing MCP server configuration.
/// </summary>
public sealed record UpdateMcpServerCommand(
    Guid Id,
    string Name,
    McpTransportType TransportType,
    string? Description,
    string? Command,
    string? Arguments,
    string? Url,
    string? EnvironmentVariablesJson) : ICommand;

internal sealed class UpdateMcpServerCommandHandler(
    IMcpServerConfigRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateMcpServerCommand>
{
    public async Task Handle(UpdateMcpServerCommand request, CancellationToken cancellationToken)
    {
        var config = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(McpServerConfig), request.Id);

        config.Name = request.Name;
        config.TransportType = request.TransportType;
        config.Description = request.Description;
        config.Command = request.Command;
        config.Arguments = request.Arguments;
        config.Url = request.Url;
        config.EnvironmentVariablesJson = request.EnvironmentVariablesJson;
        config.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(config, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
