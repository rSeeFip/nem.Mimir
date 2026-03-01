using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Entities;
using Mimir.Domain.ValueObjects;

namespace Mimir.Application.SystemPrompts.Queries;

/// <summary>
/// Query to render a system prompt template by substituting the provided variables.
/// </summary>
/// <param name="Id">The unique identifier of the system prompt to render.</param>
/// <param name="Variables">A dictionary of variable names and their replacement values.</param>
public sealed record RenderSystemPromptQuery(
    SystemPromptId Id,
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
