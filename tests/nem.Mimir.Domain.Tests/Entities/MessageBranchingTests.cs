using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public sealed class MessageBranchingTests
{
    [Fact]
    public void SetBranch_Should_Assign_Parent_And_Branch_Metadata()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Branching Test");
        var original = conversation.AddMessage(MessageRole.Assistant, "Initial response", "phi-4-mini");

        var regenerated = conversation.AddMessage(MessageRole.Assistant, "Regenerated response", "phi-4-mini");
        regenerated.SetBranch(original.Id, 1);

        regenerated.ParentMessageId.ShouldBe(original.Id);
        regenerated.BranchIndex.ShouldBe(1);
        regenerated.IsRegenerated.ShouldBeTrue();
    }

    [Fact]
    public void AddReaction_Should_Add_Reaction()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Reaction Test");
        var message = conversation.AddMessage(MessageRole.Assistant, "Hello", "phi-4-mini");
        var userId = Guid.NewGuid();

        message.AddReaction("👍", userId);

        message.Reactions.Count.ShouldBe(1);
        message.Reactions.ShouldContain(reaction => reaction.Emoji == "👍" && reaction.UserId == userId);
    }

    [Fact]
    public void AddReaction_WithSameUserAndEmoji_Should_Toggle_And_Remove()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Reaction Test");
        var message = conversation.AddMessage(MessageRole.Assistant, "Hello", "phi-4-mini");
        var userId = Guid.NewGuid();

        message.AddReaction("🔥", userId);
        message.AddReaction("🔥", userId);

        message.Reactions.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveReaction_Should_Remove_Existing_Reaction()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Reaction Test");
        var message = conversation.AddMessage(MessageRole.Assistant, "Hello", "phi-4-mini");
        var userId = Guid.NewGuid();

        message.AddReaction("🎯", userId);
        message.RemoveReaction("🎯", userId);

        message.Reactions.ShouldBeEmpty();
    }

    [Fact]
    public void Edit_Should_Allow_User_Message_Only()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Edit Test");
        var userMessage = conversation.AddMessage(MessageRole.User, "Original text");
        var assistantMessage = conversation.AddMessage(MessageRole.Assistant, "Assistant text", "phi-4-mini");

        userMessage.Edit("Updated text");

        userMessage.Content.ShouldBe("Updated text");
        Should.Throw<InvalidOperationException>(() => assistantMessage.Edit("Should fail"));
    }
}
