using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using Shouldly;
using Xunit;

namespace Mimir.Domain.Tests;

/// <summary>
/// Comprehensive negative tests for domain entity invariants, boundary conditions,
/// and invalid input handling across all domain entities.
/// </summary>
public sealed class DomainNegativeTests
{
    // ══════════════════════════════════════════════════════════════════
    // User entity — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void UserCreate_NullUsername_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => User.Create(null!, "test@example.com", UserRole.User));
    }

    [Fact]
    public void UserCreate_EmptyUsername_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => User.Create("", "test@example.com", UserRole.User));
    }

    [Fact]
    public void UserCreate_WhitespaceUsername_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => User.Create("   ", "test@example.com", UserRole.User));
    }

    [Fact]
    public void UserCreate_NullEmail_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => User.Create("validuser", null!, UserRole.User));
    }

    [Fact]
    public void UserCreate_EmptyEmail_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => User.Create("validuser", "", UserRole.User));
    }

    [Fact]
    public void UserCreate_WhitespaceEmail_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => User.Create("validuser", "   ", UserRole.User));
    }

    // ══════════════════════════════════════════════════════════════════
    // SystemPrompt entity — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void SystemPromptCreate_NullName_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => SystemPrompt.Create(null!, "template", "desc"));
    }

    [Fact]
    public void SystemPromptCreate_EmptyName_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => SystemPrompt.Create("", "template", "desc"));
    }

    [Fact]
    public void SystemPromptCreate_WhitespaceName_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => SystemPrompt.Create("   ", "template", "desc"));
    }

    [Fact]
    public void SystemPromptCreate_NullTemplate_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => SystemPrompt.Create("name", null!, "desc"));
    }

    [Fact]
    public void SystemPromptCreate_EmptyTemplate_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => SystemPrompt.Create("name", "", "desc"));
    }

    [Fact]
    public void SystemPromptCreate_WhitespaceTemplate_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => SystemPrompt.Create("name", "   ", "desc"));
    }

    [Fact]
    public void SystemPromptUpdateName_NullName_ShouldThrow()
    {
        var prompt = SystemPrompt.Create("Valid", "Template", "Desc");

        Should.Throw<ArgumentException>(() => prompt.UpdateName(null!));
    }

    [Fact]
    public void SystemPromptUpdateName_EmptyName_ShouldThrow()
    {
        var prompt = SystemPrompt.Create("Valid", "Template", "Desc");

        Should.Throw<ArgumentException>(() => prompt.UpdateName(""));
    }

    [Fact]
    public void SystemPromptUpdateName_WhitespaceName_ShouldThrow()
    {
        var prompt = SystemPrompt.Create("Valid", "Template", "Desc");

        Should.Throw<ArgumentException>(() => prompt.UpdateName("   "));
    }

    [Fact]
    public void SystemPromptUpdateTemplate_NullTemplate_ShouldThrow()
    {
        var prompt = SystemPrompt.Create("Valid", "Template", "Desc");

        Should.Throw<ArgumentException>(() => prompt.UpdateTemplate(null!));
    }

    [Fact]
    public void SystemPromptUpdateTemplate_EmptyTemplate_ShouldThrow()
    {
        var prompt = SystemPrompt.Create("Valid", "Template", "Desc");

        Should.Throw<ArgumentException>(() => prompt.UpdateTemplate(""));
    }

    // ══════════════════════════════════════════════════════════════════
    // AuditEntry entity — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void AuditEntryCreate_EmptyUserId_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            AuditEntry.Create(Guid.Empty, "Action", "Entity"));
    }

    [Fact]
    public void AuditEntryCreate_NullAction_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            AuditEntry.Create(Guid.NewGuid(), null!, "Entity"));
    }

    [Fact]
    public void AuditEntryCreate_EmptyAction_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            AuditEntry.Create(Guid.NewGuid(), "", "Entity"));
    }

    [Fact]
    public void AuditEntryCreate_WhitespaceAction_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            AuditEntry.Create(Guid.NewGuid(), "   ", "Entity"));
    }

    [Fact]
    public void AuditEntryCreate_NullEntityType_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            AuditEntry.Create(Guid.NewGuid(), "Action", null!));
    }

    [Fact]
    public void AuditEntryCreate_EmptyEntityType_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            AuditEntry.Create(Guid.NewGuid(), "Action", ""));
    }

    [Fact]
    public void AuditEntryCreate_WhitespaceEntityType_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            AuditEntry.Create(Guid.NewGuid(), "Action", "   "));
    }

    // ══════════════════════════════════════════════════════════════════
    // Message entity — negative cases (via Conversation.AddMessage)
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void ConversationAddMessage_EmptyContent_ShouldThrow()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        Should.Throw<ArgumentException>(() => conversation.AddMessage(MessageRole.User, ""));
    }

    [Fact]
    public void ConversationAddMessage_NullContent_ShouldThrow()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        Should.Throw<ArgumentException>(() => conversation.AddMessage(MessageRole.User, null!));
    }

    [Fact]
    public void ConversationAddMessage_WhitespaceContent_ShouldThrow()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        Should.Throw<ArgumentException>(() => conversation.AddMessage(MessageRole.User, "   "));
    }

    [Fact]
    public void ConversationAddMessage_ToArchivedConversation_ShouldThrow()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        conversation.Archive();

        Should.Throw<InvalidOperationException>(() =>
            conversation.AddMessage(MessageRole.User, "Should fail"));
    }

    // ══════════════════════════════════════════════════════════════════
    // Message.SetTokenCount — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void MessageSetTokenCount_NegativeCount_ShouldThrow()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.User, "Hello");

        Should.Throw<ArgumentException>(() => message.SetTokenCount(-1));
    }

    [Fact]
    public void MessageSetTokenCount_ZeroCount_ShouldSucceed()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.User, "Hello");

        message.SetTokenCount(0);

        message.TokenCount.ShouldBe(0);
    }

    [Fact]
    public void MessageSetTokenCount_LargeNegativeCount_ShouldThrow()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.User, "Hello");

        Should.Throw<ArgumentException>(() => message.SetTokenCount(int.MinValue));
    }

    // ══════════════════════════════════════════════════════════════════
    // Conversation state transitions — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void ConversationUpdateTitle_NullTitle_ShouldThrow()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Original");

        Should.Throw<ArgumentException>(() => conversation.UpdateTitle(null!));
    }

    [Fact]
    public void ConversationCreate_WithEmptyGuidUserId_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => Conversation.Create(Guid.Empty, "Title"));
    }
}
