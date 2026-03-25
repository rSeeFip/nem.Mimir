using nem.Mimir.Application.Common.Mappings;
using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Conversations.Commands;

/// <summary>
/// Command to create a new conversation for the current user.
/// </summary>
/// <param name="Title">The title of the new conversation.</param>
/// <param name="SystemPrompt">An optional system prompt to associate with the conversation.</param>
/// <param name="Model">An optional LLM model identifier to use for the conversation.</param>
public sealed record CreateConversationCommand(
    string Title,
    string? SystemPrompt,
    string? Model) : ICommand<ConversationDto>;

/// <summary>
/// Validates the <see cref="CreateConversationCommand"/> ensuring the title is provided and within length limits.
/// </summary>
public sealed class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");
    }
}

internal sealed class CreateConversationCommandHandler(
    IConversationRepository repository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    MimirMapper mapper)
{

    public async Task<ConversationDto> Handle(CreateConversationCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
        {
            throw new ForbiddenAccessException("User identity could not be determined.");
        }

        var conversation = Conversation.Create(userGuid, request.Title);

        await repository.CreateAsync(conversation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.MapToConversationDto(conversation);
    }
}
