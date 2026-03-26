using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.McpServers.Dtos;
using nem.Mimir.Domain.McpServers;

namespace nem.Mimir.Application.McpServers.Queries;

public sealed record GetMcpPromptQuery(Guid ServerId, string PromptName, IReadOnlyDictionary<string, string>? Arguments = null) : IQuery<McpPromptResultDto>;

internal sealed class GetMcpPromptQueryHandler(
    IMcpServerConfigRepository repository,
    IMcpClientManager clientManager) : IRequestHandler<GetMcpPromptQuery, McpPromptResultDto>
{
    public async Task<McpPromptResultDto> Handle(GetMcpPromptQuery request, CancellationToken cancellationToken)
    {
        _ = await repository.GetByIdAsync(request.ServerId, cancellationToken)
            ?? throw new NotFoundException(nameof(McpServerConfig), request.ServerId);

        var result = await clientManager.GetPromptAsync(request.ServerId, request.PromptName, request.Arguments, cancellationToken);

        var messages = result.Messages
            .Select(m => new McpPromptMessageDto(m.Role, m.Content))
            .ToList();

        return new McpPromptResultDto(result.Description, messages);
    }
}
