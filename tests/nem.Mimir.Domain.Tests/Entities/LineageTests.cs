using System.Linq;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.Events;
using nem.Mimir.Domain.Services;
using Shouldly;
using Xunit;

namespace nem.Mimir.Domain.Tests.Entities;

public class LineageTests
{
    [Fact]
    public void Fork_Should_Create_Conversation_With_Valid_ParentConversationId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var parent = Conversation.Create(userId, "Parent Conversation");

        // Act
        var forked = parent.Fork(userId, "Branch for experimentation");

        // Assert
        forked.ParentConversationId.ShouldBe(parent.Id);
        forked.Id.ShouldNotBe(parent.Id);
        forked.Id.ShouldNotBe(Guid.Empty);
        forked.Status.ShouldBe(ConversationStatus.Active);
        forked.Title.ShouldBe("Parent Conversation (fork)");
    }

    [Fact]
    public void Fork_Should_Preserve_Provided_UserId()
    {
        // Arrange
        var parentUserId = Guid.NewGuid();
        var forkUserId = Guid.NewGuid();
        var parent = Conversation.Create(parentUserId, "Parent Conversation");

        // Act
        var forked = parent.Fork(forkUserId, "Forked by different user");

        // Assert
        forked.UserId.ShouldBe(forkUserId);
        parent.UserId.ShouldBe(parentUserId);
    }

    [Fact]
    public void Fork_Should_Store_ForkReason()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var parent = Conversation.Create(userId, "Parent Conversation");
        var reason = "Exploring alternative approach";

        // Act
        var forked = parent.Fork(userId, reason);

        // Assert
        forked.ForkReason.ShouldBe(reason);
    }

    [Fact]
    public void Fork_Should_Raise_ConversationForkedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var parent = Conversation.Create(userId, "Parent Conversation");
        var reason = "Branch for experimentation";

        // Act
        var forked = parent.Fork(userId, reason);

        // Assert
        var domainEvent = forked.DomainEvents.ShouldHaveSingleItem();
        domainEvent.ShouldBeOfType<ConversationForkedEvent>();
        var forkedEvent = (ConversationForkedEvent)domainEvent;
        forkedEvent.ConversationId.ShouldBe(forked.Id);
        forkedEvent.ParentConversationId.ShouldBe(parent.Id);
        forkedEvent.ForkReason.ShouldBe(reason);
        forkedEvent.UserId.ShouldBe(userId);
    }

    [Fact]
    public void Root_Conversation_Should_Have_Null_ParentConversationId()
    {
        // Arrange & Act
        var conversation = Conversation.Create(Guid.NewGuid(), "Root Conversation");

        // Assert
        conversation.ParentConversationId.ShouldBeNull();
        conversation.ForkReason.ShouldBeNull();
    }

    [Fact]
    public void Fork_With_Empty_UserId_Should_Throw()
    {
        // Arrange
        var parent = Conversation.Create(Guid.NewGuid(), "Parent Conversation");

        // Act & Assert
        Should.Throw<ArgumentException>(() => parent.Fork(Guid.Empty, "reason"));
    }

    [Fact]
    public void Fork_With_Empty_ForkReason_Should_Throw()
    {
        // Arrange
        var parent = Conversation.Create(Guid.NewGuid(), "Parent Conversation");

        // Act & Assert
        Should.Throw<ArgumentException>(() => parent.Fork(Guid.NewGuid(), string.Empty));
    }

    [Fact]
    public void Fork_With_Whitespace_ForkReason_Should_Throw()
    {
        // Arrange
        var parent = Conversation.Create(Guid.NewGuid(), "Parent Conversation");

        // Act & Assert
        Should.Throw<ArgumentException>(() => parent.Fork(Guid.NewGuid(), "   "));
    }

    [Fact]
    public void Multiple_Forks_From_Same_Parent_Should_Create_Distinct_Conversations()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var parent = Conversation.Create(userId, "Parent Conversation");

        // Act
        var fork1 = parent.Fork(userId, "Branch A");
        var fork2 = parent.Fork(userId, "Branch B");
        var fork3 = parent.Fork(userId, "Branch C");

        // Assert
        fork1.Id.ShouldNotBe(fork2.Id);
        fork2.Id.ShouldNotBe(fork3.Id);
        fork1.Id.ShouldNotBe(fork3.Id);
        fork1.ParentConversationId.ShouldBe(parent.Id);
        fork2.ParentConversationId.ShouldBe(parent.Id);
        fork3.ParentConversationId.ShouldBe(parent.Id);
        fork1.ForkReason.ShouldBe("Branch A");
        fork2.ForkReason.ShouldBe("Branch B");
        fork3.ForkReason.ShouldBe("Branch C");
    }

    [Fact]
    public void Lineage_BuildLineageTree_Should_Build_Correct_Tree()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var root = Conversation.Create(userId, "Root");
        var child1 = root.Fork(userId, "Fork 1");
        var child2 = root.Fork(userId, "Fork 2");
        var grandchild = child1.Fork(userId, "Fork 1.1");

        var conversations = new[] { root, child1, child2, grandchild };

        // Act
        var roots = SessionLineageService.BuildLineageTree(conversations);

        // Assert
        roots.Count.ShouldBe(1);
        var rootNode = roots[0];
        rootNode.ConversationId.ShouldBe(root.Id);
        rootNode.ParentConversationId.ShouldBeNull();
        rootNode.Children.Count.ShouldBe(2);

        var child1Node = rootNode.Children.First(c => c.ConversationId == child1.Id);
        child1Node.ForkReason.ShouldBe("Fork 1");
        child1Node.Children.Count.ShouldBe(1);
        child1Node.Children[0].ConversationId.ShouldBe(grandchild.Id);

        var child2Node = rootNode.Children.First(c => c.ConversationId == child2.Id);
        child2Node.ForkReason.ShouldBe("Fork 2");
        child2Node.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Lineage_BuildLineageTree_With_Orphan_Should_Treat_As_Root()
    {
        // Arrange — child whose parent is NOT in the collection
        var userId = Guid.NewGuid();
        var root = Conversation.Create(userId, "Root");
        var child = root.Fork(userId, "Fork");

        // Only include the child, not the parent
        var conversations = new[] { child };

        // Act
        var roots = SessionLineageService.BuildLineageTree(conversations);

        // Assert — orphan becomes a root
        roots.Count.ShouldBe(1);
        roots[0].ConversationId.ShouldBe(child.Id);
        roots[0].ParentConversationId.ShouldBe(root.Id); // still records the parent ID
    }

    [Fact]
    public void Lineage_GetAncestors_Should_Return_Chain_To_Root()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var root = Conversation.Create(userId, "Root");
        var child = root.Fork(userId, "Fork 1");
        var grandchild = child.Fork(userId, "Fork 1.1");

        var conversations = new[] { root, child, grandchild };

        // Act
        var ancestors = SessionLineageService.GetAncestors(grandchild.Id, conversations);

        // Assert
        ancestors.Count.ShouldBe(3);
        ancestors[0].ShouldBe(grandchild.Id);
        ancestors[1].ShouldBe(child.Id);
        ancestors[2].ShouldBe(root.Id);
    }

    [Fact]
    public void Lineage_GetAncestors_For_Root_Should_Return_Single_Element()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var root = Conversation.Create(userId, "Root");
        var conversations = new[] { root };

        // Act
        var ancestors = SessionLineageService.GetAncestors(root.Id, conversations);

        // Assert
        ancestors.Count.ShouldBe(1);
        ancestors[0].ShouldBe(root.Id);
    }

    [Fact]
    public void Lineage_GetDescendants_Should_Return_All_Children()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var root = Conversation.Create(userId, "Root");
        var child1 = root.Fork(userId, "Fork 1");
        var child2 = root.Fork(userId, "Fork 2");
        var grandchild = child1.Fork(userId, "Fork 1.1");

        var conversations = new[] { root, child1, child2, grandchild };

        // Act
        var descendants = SessionLineageService.GetDescendants(root.Id, conversations);

        // Assert
        descendants.Count.ShouldBe(3);
        descendants.ShouldContain(child1.Id);
        descendants.ShouldContain(child2.Id);
        descendants.ShouldContain(grandchild.Id);
    }

    [Fact]
    public void Lineage_GetDescendants_For_Leaf_Should_Return_Empty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var root = Conversation.Create(userId, "Root");
        var child = root.Fork(userId, "Fork 1");
        var conversations = new[] { root, child };

        // Act
        var descendants = SessionLineageService.GetDescendants(child.Id, conversations);

        // Assert
        descendants.ShouldBeEmpty();
    }

    [Fact]
    public void Lineage_BuildLineageTree_With_Empty_Collection_Should_Return_Empty()
    {
        // Act
        var roots = SessionLineageService.BuildLineageTree(Array.Empty<Conversation>());

        // Assert
        roots.ShouldBeEmpty();
    }

    [Fact]
    public void Lineage_GetAncestors_With_Unknown_Id_Should_Return_Empty()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        var conversations = new[] { Conversation.Create(Guid.NewGuid(), "Some Conversation") };

        // Act
        var ancestors = SessionLineageService.GetAncestors(unknownId, conversations);

        // Assert
        ancestors.ShouldBeEmpty();
    }

    [Fact]
    public void Fork_Should_Not_Copy_Messages_From_Parent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var parent = Conversation.Create(userId, "Parent");
        parent.AddMessage(MessageRole.User, "Hello");
        parent.AddMessage(MessageRole.Assistant, "Hi there");

        // Act
        var forked = parent.Fork(userId, "Clean fork");

        // Assert
        parent.Messages.Count.ShouldBe(2);
        forked.Messages.ShouldBeEmpty();
    }
}
