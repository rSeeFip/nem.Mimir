using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Services;

namespace nem.Mimir.Application.Conversations.Queries;

public sealed record LineageGraphDto(
    Guid ConversationId,
    string Title,
    Guid? ParentConversationId,
    string? ForkReason,
    List<LineageGraphDto> Children);

public sealed record GetLineageGraphQuery(Guid ConversationId) : IQuery<LineageGraphDto>;

public sealed class GetLineageGraphQueryValidator : AbstractValidator<GetLineageGraphQuery>
{
    public GetLineageGraphQueryValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");
    }
}

internal sealed class GetLineageGraphQueryHandler(
    IConversationRepository repository,
    ICurrentUserService currentUserService)
{
    public async Task<LineageGraphDto> Handle(GetLineageGraphQuery query, CancellationToken ct)
    {
        var userId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
        {
            throw new ForbiddenAccessException("User identity could not be determined.");
        }

        var allUserConversations = await repository.GetAllByUserIdAsync(userGuid, ct);
        var conversationsById = allUserConversations.ToDictionary(c => c.Id);

        if (!conversationsById.TryGetValue(query.ConversationId, out var targetConversation))
        {
            throw new NotFoundException(nameof(Conversation), query.ConversationId);
        }

        var lineageRoots = SessionLineageService.BuildLineageTree(allUserConversations);

        var lineageRootId = SessionLineageService.GetAncestors(targetConversation.Id, allUserConversations).LastOrDefault();
        if (lineageRootId == Guid.Empty)
        {
            throw new NotFoundException(nameof(Conversation), query.ConversationId);
        }

        var rootNode = lineageRoots.FirstOrDefault(x => x.ConversationId == lineageRootId)
            ?? throw new NotFoundException(nameof(Conversation), query.ConversationId);

        return MapNode(rootNode, conversationsById);
    }

    private static LineageGraphDto MapNode(
        SessionLineageService.LineageNode node,
        IReadOnlyDictionary<Guid, Conversation> conversationsById)
    {
        var conversation = conversationsById[node.ConversationId];

        return new LineageGraphDto(
            node.ConversationId,
            conversation.Title,
            node.ParentConversationId,
            node.ForkReason,
            node.Children.Select(child => MapNode(child, conversationsById)).ToList());
    }
}
