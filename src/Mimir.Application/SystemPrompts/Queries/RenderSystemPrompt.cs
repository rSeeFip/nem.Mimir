using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Entities;

namespace Mimir.Application.SystemPrompts.Queries;

public sealed record RenderSystemPromptQuery(
    Guid Id,
    IDictionary<string, string> Variables) : IQuery<string>;

internal sealed class RenderSystemPromptQueryHandler : IRequestHandler<RenderSystemPromptQuery, string>
{
    private readonly ISystemPromptRepository _repository;
    private readonly ISystemPromptService _systemPromptService;

    public RenderSystemPromptQueryHandler(
        ISystemPromptRepository repository,
        ISystemPromptService systemPromptService)
    {
        _repository = repository;
        _systemPromptService = systemPromptService;
    }

    public async Task<string> Handle(RenderSystemPromptQuery request, CancellationToken cancellationToken)
    {
        var prompt = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SystemPrompt), request.Id);

        return _systemPromptService.RenderTemplate(prompt.Template, request.Variables);
    }
}
