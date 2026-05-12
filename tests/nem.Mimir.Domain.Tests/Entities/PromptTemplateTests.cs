using nem.Mimir.Domain.Entities;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public sealed class PromptTemplateTests
{
    [Fact]
    public void Create_WithValidValues_ShouldInitializeExpectedState()
    {
        var userId = Guid.NewGuid();

        var template = PromptTemplate.Create(
            userId,
            "Summarize article",
            "/summarize",
            "Summarize {{topic}} in {{tone}} tone.",
            ["Writing", "Writing", "Productivity"],
            isShared: true);

        template.Id.ShouldNotBe(default);
        template.UserId.ShouldBe(userId);
        template.Title.ShouldBe("Summarize article");
        template.Command.ShouldBe("/summarize");
        template.Content.ShouldBe("Summarize {{topic}} in {{tone}} tone.");
        template.IsShared.ShouldBeTrue();
        template.UsageCount.ShouldBe(0);
        template.Tags.Count.ShouldBe(2);
        template.Tags.ShouldContain("writing");
        template.Tags.ShouldContain("productivity");
        template.VersionHistory.Count.ShouldBe(1);
    }

    [Fact]
    public void Create_WithInvalidCommand_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            PromptTemplate.Create(Guid.NewGuid(), "Name", "invalid-command", "content"));
    }

    [Fact]
    public void Update_ShouldAlwaysAppendVersionEntry()
    {
        var template = PromptTemplate.Create(Guid.NewGuid(), "Name", "/command", "v1 content");

        template.Update("Updated", "/command", "v1 content");

        template.VersionHistory.Count.ShouldBe(2);
        template.VersionHistory.Last().Content.ShouldBe("v1 content");
    }

    [Fact]
    public void AddAndRemoveTag_ShouldNormalizeAndMutateTagCollection()
    {
        var template = PromptTemplate.Create(Guid.NewGuid(), "Name", "/command", "content");

        template.AddTag("Ops");
        template.AddTag("ops");
        template.AddTag("  Team  ");
        template.RemoveTag("OPS");

        template.Tags.Count.ShouldBe(1);
        template.Tags.ShouldContain("team");
        template.Tags.ShouldNotContain("ops");
    }

    [Fact]
    public void IncrementUsage_ShouldIncreaseUsageCount()
    {
        var template = PromptTemplate.Create(Guid.NewGuid(), "Name", "/command", "content");

        template.IncrementUsage();
        template.IncrementUsage();

        template.UsageCount.ShouldBe(2);
    }
}
