using MediatR;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.McpServers.Dtos;
using nem.Mimir.Domain.McpServers;

namespace nem.Mimir.Application.McpServers.Queries;

public sealed record GetMcpResourcesQuery(Guid ServerId) : IQuery<GetMcpResourcesResult>;

public sealed record GetMcpResourcesResult(
    IReadOnlyList<McpResourceDto> Resources,
    IReadOnlyList<McpResourceTemplateDto> Templates);

internal sealed class GetMcpResourcesQueryHandler(
    IMcpServerConfigRepository repository,
    IMcpClientManager clientManager,
    ILogger<GetMcpResourcesQueryHandler> logger) : IRequestHandler<GetMcpResourcesQuery, GetMcpResourcesResult>
{
    public async Task<GetMcpResourcesResult> Handle(GetMcpResourcesQuery request, CancellationToken cancellationToken)
    {
        _ = await repository.GetByIdAsync(request.ServerId, cancellationToken)
            ?? throw new NotFoundException(nameof(McpServerConfig), request.ServerId);

        try
        {
            var resources = await clientManager.ListResourcesAsync(request.ServerId, cancellationToken);
            var templates = await clientManager.ListResourceTemplatesAsync(request.ServerId, cancellationToken);

            var resourceDtos = resources
                .Select(r => new McpResourceDto(r.Uri, r.Name, r.Description, r.MimeType))
                .ToList();

            var templateDtos = templates
                .Select(t => new McpResourceTemplateDto(t.UriTemplate, t.Name, t.Description, t.MimeType))
                .ToList();

            return new GetMcpResourcesResult(resourceDtos, templateDtos);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve resources for MCP server {ServerId}. Server may not be connected.", request.ServerId);
            return new GetMcpResourcesResult([], []);
        }
    }
}
