using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.McpServers.Dtos;
using nem.Mimir.Domain.McpServers;

namespace nem.Mimir.Application.McpServers.Queries;

public sealed record ReadMcpResourceQuery(Guid ServerId, string Uri) : IQuery<IReadOnlyList<McpResourceContentDto>>;

internal sealed class ReadMcpResourceQueryHandler(
    IMcpServerConfigRepository repository,
    IMcpClientManager clientManager) : IRequestHandler<ReadMcpResourceQuery, IReadOnlyList<McpResourceContentDto>>
{
    public async Task<IReadOnlyList<McpResourceContentDto>> Handle(ReadMcpResourceQuery request, CancellationToken cancellationToken)
    {
        _ = await repository.GetByIdAsync(request.ServerId, cancellationToken)
            ?? throw new NotFoundException(nameof(McpServerConfig), request.ServerId);

        var contents = await clientManager.ReadResourceAsync(request.ServerId, request.Uri, cancellationToken);

        return contents
            .Select(c => new McpResourceContentDto(c.Uri, c.MimeType, c.Text, c.Blob))
            .ToList();
    }
}
