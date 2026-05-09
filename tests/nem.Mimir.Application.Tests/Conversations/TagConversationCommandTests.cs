using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class TagConversationCommandTests
{
    [Fact]
    public async Task Handle_ShouldAddTag()
    {
        var repository = Substitute.For<IConversationRepository>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Tagged conversation");

        currentUserService.UserId.Returns(userId.ToString());
        repository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);

        var handler = new TagConversationCommandHandler(repository, currentUserService, unitOfWork);

        await handler.Handle(new TagConversationCommand(conversation.Id, "Important"), CancellationToken.None);

        conversation.Tags.ShouldContain("important");
        await repository.Received(1).UpdateAsync(conversation, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
