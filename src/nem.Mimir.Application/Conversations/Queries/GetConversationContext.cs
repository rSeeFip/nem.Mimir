using FluentValidation;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Services;

namespace nem.Mimir.Application.Conversations.Queries;

public sealed record GetConversationContextQuery(
    Guid ConversationId,
    string Query,
    int MaxResults = 10) : IQuery<ConversationContextDto>;

public sealed class GetConversationContextQueryValidator : AbstractValidator<GetConversationContextQuery>
{
    public GetConversationContextQueryValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Query is required.")
            .MaximumLength(1000).WithMessage("Query must not exceed 1000 characters.");

        RuleFor(x => x.MaxResults)
            .InclusiveBetween(1, 100).WithMessage("Max results must be between 1 and 100.");
    }
}

internal sealed class GetConversationContextQueryHandler
{
    private readonly IConversationContextService _conversationRagService;

    public GetConversationContextQueryHandler(IConversationContextService conversationRagService)
    {
        _conversationRagService = conversationRagService;
    }

    public async Task<ConversationContextDto> Handle(GetConversationContextQuery request, CancellationToken cancellationToken)
    {
        var results = await _conversationRagService
            .GetRagContextAsync(request.ConversationId, request.Query, cancellationToken)
            .ConfigureAwait(false);

        return new ConversationContextDto(request.ConversationId, request.Query, results.Take(request.MaxResults).ToList());
    }
}
