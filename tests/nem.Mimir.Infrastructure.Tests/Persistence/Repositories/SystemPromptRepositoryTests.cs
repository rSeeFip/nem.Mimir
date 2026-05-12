using Microsoft.EntityFrameworkCore;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Infrastructure.Persistence.Repositories;
using nem.Mimir.Domain.ValueObjects;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Persistence.Repositories;

public sealed class SystemPromptRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task CreateAsync_AddsPromptToContext()
    {
        // Arrange
        var repository = new SystemPromptRepository(Context);
        var prompt = SystemPrompt.Create("Test Prompt", "Hello {{name}}", "A test prompt");

        // Act
        var result = await repository.CreateAsync(prompt);
        await Context.SaveChangesAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Test Prompt");

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.SystemPrompts.FindAsync(prompt.Id);
        stored.ShouldNotBeNull();
        stored.Name.ShouldBe("Test Prompt");
        stored.Template.ShouldBe("Hello {{name}}");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsPromptWhenExists()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Test", "Template", "Desc");
        Context.SystemPrompts.Add(prompt);
        await Context.SaveChangesAsync();

        await using var queryContext = CreateContext();
        var repository = new SystemPromptRepository(queryContext);

        // Act
        var result = await repository.GetByIdAsync(prompt.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(prompt.Id);
        result.Name.ShouldBe("Test");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        var repository = new SystemPromptRepository(Context);

        // Act
        var result = await repository.GetByIdAsync(SystemPromptId.New());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPaginatedActivePrompts()
    {
        // Arrange
        var prompt1 = SystemPrompt.Create("P1", "T1", "D1");
        var prompt2 = SystemPrompt.Create("P2", "T2", "D2");
        var prompt3 = SystemPrompt.Create("P3", "T3", "D3");
        prompt3.Deactivate();

        Context.SystemPrompts.AddRange(prompt1, prompt2, prompt3);
        await Context.SaveChangesAsync();

        await using var queryContext = CreateContext();
        var repository = new SystemPromptRepository(queryContext);

        // Act
        var result = await repository.GetAllAsync(1, 10);

        // Assert — only active prompts returned
        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesPrompt()
    {
        // Arrange
        var prompt = SystemPrompt.Create("ToDelete", "Template", "Desc");
        Context.SystemPrompts.Add(prompt);
        await Context.SaveChangesAsync();

        await using var deleteContext = CreateContext();
        var repository = new SystemPromptRepository(deleteContext);

        // Act
        await repository.DeleteAsync(prompt.Id);
        await deleteContext.SaveChangesAsync();

        // Assert — query filter excludes soft-deleted prompts
        await using var verifyContext = CreateContext();
        var filtered = await verifyContext.SystemPrompts
            .FirstOrDefaultAsync(sp => sp.Id == prompt.Id);
        filtered.ShouldBeNull();

        // But the row still exists with IsDeleted = true
        var raw = await verifyContext.SystemPrompts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(sp => sp.Id == prompt.Id);
        raw.ShouldNotBeNull();
        raw.IsDeleted.ShouldBeTrue();
        raw.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_IsNoopWhenNotFound()
    {
        // Arrange
        var repository = new SystemPromptRepository(Context);

        // Act & Assert — should not throw
        await repository.DeleteAsync(SystemPromptId.New());
    }

    [Fact]
    public async Task GetDefaultAsync_ReturnsDefaultPrompt()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Default", "Template", "Desc");
        prompt.SetAsDefault();
        Context.SystemPrompts.Add(prompt);
        await Context.SaveChangesAsync();

        await using var queryContext = CreateContext();
        var repository = new SystemPromptRepository(queryContext);

        // Act
        var result = await repository.GetDefaultAsync();

        // Assert
        result.ShouldNotBeNull();
        result.IsDefault.ShouldBeTrue();
        result.Name.ShouldBe("Default");
    }

    [Fact]
    public async Task GetDefaultAsync_ReturnsNullWhenNoDefault()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Not Default", "Template", "Desc");
        Context.SystemPrompts.Add(prompt);
        await Context.SaveChangesAsync();

        await using var queryContext = CreateContext();
        var repository = new SystemPromptRepository(queryContext);

        // Act
        var result = await repository.GetDefaultAsync();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesPrompt()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Original", "Original Template", "Desc");
        Context.SystemPrompts.Add(prompt);
        await Context.SaveChangesAsync();

        await using var updateContext = CreateContext();
        var repository = new SystemPromptRepository(updateContext);
        var toUpdate = await updateContext.SystemPrompts.FindAsync(prompt.Id);
        toUpdate!.UpdateName("Updated");
        toUpdate.UpdateTemplate("Updated Template");

        // Act
        await repository.UpdateAsync(toUpdate);
        await updateContext.SaveChangesAsync();

        // Assert
        await using var verifyContext = CreateContext();
        var result = await verifyContext.SystemPrompts.FindAsync(prompt.Id);
        result!.Name.ShouldBe("Updated");
        result.Template.ShouldBe("Updated Template");
    }
}
