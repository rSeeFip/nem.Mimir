using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;
using Mimir.Domain.ValueObjects;

namespace Mimir.Application.SystemPrompts.Queries;

/// <summary>
/// Query to retrieve a system prompt by its unique identifier.
/// </summary>
/// <param name="Id">The unique identifier of the system prompt to retrieve.</param>
public sealed record GetSystemPromptByIdQuery(SystemPromptId Id) : IQuery<SystemPromptDto>;

internal sealed class GetSystemPromptByIdQueryHandler : IRequestHandler<GetSystemPromptByIdQuery, SystemPromptDto>
{
    private readonly ISystemPromptRepository _repository;
    private readonly MimirMapper _mapper;

    public GetSystemPromptByIdQueryHandler(
        ISystemPromptRepository repository,
        MimirMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<SystemPromptDto> Handle(GetSystemPromptByIdQuery request, CancellationToken cancellationToken)
    {
        var prompt = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SystemPrompt), request.Id);

        return _mapper.MapToSystemPromptDto(prompt);
    }
}
