using AutoMapper;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;

namespace Mimir.Application.SystemPrompts.Queries;

public sealed record GetSystemPromptByIdQuery(Guid Id) : IQuery<SystemPromptDto>;

internal sealed class GetSystemPromptByIdQueryHandler : IRequestHandler<GetSystemPromptByIdQuery, SystemPromptDto>
{
    private readonly ISystemPromptRepository _repository;
    private readonly IMapper _mapper;

    public GetSystemPromptByIdQueryHandler(
        ISystemPromptRepository repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<SystemPromptDto> Handle(GetSystemPromptByIdQuery request, CancellationToken cancellationToken)
    {
        var prompt = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SystemPrompt), request.Id);

        return _mapper.Map<SystemPromptDto>(prompt);
    }
}
