using MediatR;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.McpServers.Dtos;
using nem.Mimir.Domain.McpServers;

namespace nem.Mimir.Application.McpServers.Queries;

public sealed record GetMcpPromptsQuery(Guid ServerId) : IQuery<IReadOnlyList<McpPromptDto>>;

internal sealed class GetMcpPromptsQueryHandler(
    IMcpServerConfigRepository repository,
    IMcpClientManager clientManager,
    ILogger<GetMcpPromptsQueryHandler> logger) : IRequestHandler<GetMcpPromptsQuery, IReadOnlyList<McpPromptDto>>
{
    public async Task<IReadOnlyList<McpPromptDto>> Handle(GetMcpPromptsQuery request, CancellationToken cancellationToken)
    {
        _ = await repository.GetByIdAsync(request.ServerId, cancellationToken)
            ?? throw new NotFoundException(nameof(McpServerConfig), request.ServerId);

        try
        {
            var prompts = await clientManager.ListPromptsAsync(request.ServerId, cancellationToken);

            return prompts
                .Select(p => new McpPromptDto(
                    p.Name,
                    p.Description,
                    p.Arguments?
                        .Select(a => new McpPromptArgumentDto(a.Name, a.Description, a.Required))
                        .ToList()))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve prompts for MCP server {ServerId}. Server may not be connected.", request.ServerId);
            return [];
        }
    }
}
