using MediatR;
using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.McpServers.Dtos;
using Mimir.Domain.McpServers;

namespace Mimir.Application.McpServers.Queries;

/// <summary>
/// Query to list tools exposed by a connected MCP server.
/// </summary>
public sealed record GetMcpServerToolsQuery(Guid Id) : IQuery<IReadOnlyList<McpServerToolDto>>;

internal sealed class GetMcpServerToolsQueryHandler(
    IMcpServerConfigRepository repository,
    IMcpClientManager clientManager,
    ILogger<GetMcpServerToolsQueryHandler> logger) : IRequestHandler<GetMcpServerToolsQuery, IReadOnlyList<McpServerToolDto>>
{
    public async Task<IReadOnlyList<McpServerToolDto>> Handle(GetMcpServerToolsQuery request, CancellationToken cancellationToken)
    {
        _ = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(McpServerConfig), request.Id);

        try
        {
            var tools = await clientManager.GetServerToolsAsync(request.Id, cancellationToken);

            return tools
                .Select(t => new McpServerToolDto(t.Name, t.Description, t.ParametersJsonSchema))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve tools for MCP server {ServerId}. Server may not be connected.", request.Id);
            return [];
        }
    }
}
