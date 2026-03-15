using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.McpServers;

namespace nem.Mimir.Application.McpServers.Commands;

/// <summary>
/// Represents a single whitelist entry to set for a tool.
/// </summary>
public sealed record ToolWhitelistEntry(
    string ToolName,
    bool IsEnabled);

/// <summary>
/// Command to replace the tool whitelist entries for an MCP server.
/// </summary>
public sealed record UpdateToolWhitelistCommand(
    Guid ServerId,
    IReadOnlyList<ToolWhitelistEntry> Entries) : ICommand;

internal sealed class UpdateToolWhitelistCommandHandler(
    IMcpServerConfigRepository repository,
    IToolWhitelistService whitelistService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateToolWhitelistCommand>
{
    public async Task Handle(UpdateToolWhitelistCommand request, CancellationToken cancellationToken)
    {
        var config = await repository.GetByIdAsync(request.ServerId, cancellationToken)
            ?? throw new NotFoundException(nameof(McpServerConfig), request.ServerId);

        foreach (var entry in request.Entries)
        {
            await whitelistService.SetToolWhitelistAsync(
                config.Id, entry.ToolName, entry.IsEnabled, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
