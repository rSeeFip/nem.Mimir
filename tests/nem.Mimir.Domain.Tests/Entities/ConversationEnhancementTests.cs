using nem.Mimir.Domain.Entities;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public class ConversationEnhancementTests
{
    [Fact]
    public void PinAndUnpin_ShouldTogglePinnedState()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        conversation.Pin();
        conversation.IsPinned.ShouldBeTrue();

        conversation.Unpin();
        conversation.IsPinned.ShouldBeFalse();
    }

    [Fact]
    public void Share_ShouldGenerateShareId()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        conversation.Share();

        conversation.ShareId.ShouldNotBeNull();
        conversation.ShareId!.Length.ShouldBe(12);
    }

    [Fact]
    public void Unshare_ShouldClearShareId()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        conversation.Share();

        conversation.Unshare();

        conversation.ShareId.ShouldBeNull();
    }

    [Fact]
    public void AddAndRemoveTag_ShouldManageTagCollection()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        conversation.AddTag("Work");
        conversation.Tags.ShouldContain("work");

        conversation.RemoveTag("WORK");
        conversation.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void MoveToFolder_ShouldSetFolderId()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var folderId = Guid.NewGuid();

        conversation.MoveToFolder(folderId);
        conversation.FolderId.ShouldBe(folderId);

        conversation.MoveToFolder(null);
        conversation.FolderId.ShouldBeNull();
    }
}
