using FluentValidation;
using nem.Contracts.Identity;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Prompts.Commands;

public sealed record CreatePromptTemplateCommand(
    string Title,
    string Command,
    string Content,
    IReadOnlyList<string>? Tags,
    bool IsShared) : ICommand<PromptTemplateDto>;

public sealed class CreatePromptTemplateCommandValidator : AbstractValidator<CreatePromptTemplateCommand>
{
    public CreatePromptTemplateCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Prompt template title is required.")
            .MaximumLength(200).WithMessage("Prompt template title must not exceed 200 characters.");

        RuleFor(x => x.Command)
            .NotEmpty().WithMessage("Prompt template command is required.")
            .Matches("^/[a-z0-9-]+$").WithMessage("Prompt template command must match '/command-name'.")
            .MaximumLength(100).WithMessage("Prompt template command must not exceed 100 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Prompt template content is required.")
            .MaximumLength(20000).WithMessage("Prompt template content must not exceed 20000 characters.");

        RuleForEach(x => x.Tags)
            .MaximumLength(ConversationTag.MaxLength).WithMessage($"Tag cannot exceed {ConversationTag.MaxLength} characters.");
    }
}

internal sealed class CreatePromptTemplateCommandHandler
{
    private readonly IPromptTemplateRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;

    public CreatePromptTemplateCommandHandler(
        IPromptTemplateRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PromptTemplateDto> Handle(CreatePromptTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var existing = await _repository
            .GetByCommandForUserAsync(request.Command, userId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            throw new ConflictException($"A prompt template with command '{request.Command}' already exists.");
        }

        var promptTemplate = PromptTemplate.Create(
            userId,
            request.Title,
            request.Command,
            request.Content,
            request.Tags,
            request.IsShared);

        await _repository.CreateAsync(promptTemplate, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return _mapper.MapToPromptTemplateDto(promptTemplate);
    }

    private static Guid ResolveCurrentUserId(ICurrentUserService currentUserService)
    {
        var userId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new ForbiddenAccessException("Current user identifier is invalid.");

        return parsedUserId;
    }
}
