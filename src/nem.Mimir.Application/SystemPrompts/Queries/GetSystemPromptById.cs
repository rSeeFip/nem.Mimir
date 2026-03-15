using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.SystemPrompts.Queries;

/// <summary>
/// Query to retrieve a system prompt by its unique identifier.
/// </summary>
/// <param name="Id">The unique identifier of the system prompt to retrieve.</param>
public sealed record GetSystemPromptByIdQuery(Guid Id) : IQuery<SystemPromptDto>;

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
