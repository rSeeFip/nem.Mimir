using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public sealed class UserMemoryTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_UserMemory()
    {
        var userId = Guid.NewGuid();
        var content = "Remember my preference for concise responses.";
        var context = "chat settings";

        var memory = UserMemory.Create(userId, content, context);

        memory.Id.ShouldNotBe(UserMemoryId.Empty);
        memory.UserId.ShouldBe(userId);
        memory.Content.ShouldBe(content);
        memory.Context.ShouldBe(context);
        memory.Source.ShouldBe("manual");
        memory.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_With_Empty_UserId_Should_Throw()
    {
        Should.Throw<ArgumentException>(() =>
            UserMemory.Create(Guid.Empty, "content", "context"));
    }

    [Fact]
    public void Create_With_Empty_Content_Should_Throw()
    {
        Should.Throw<ArgumentException>(() =>
            UserMemory.Create(Guid.NewGuid(), string.Empty, "context"));
    }

    [Fact]
    public void Update_With_Valid_Values_Should_Update_Content_And_Context()
    {
        var memory = UserMemory.Create(Guid.NewGuid(), "old", "old ctx");

        memory.Update("new content", "new ctx");

        memory.Content.ShouldBe("new content");
        memory.Context.ShouldBe("new ctx");
    }

    [Fact]
    public void Update_With_Empty_Content_Should_Throw()
    {
        var memory = UserMemory.Create(Guid.NewGuid(), "content", null);

        Should.Throw<ArgumentException>(() => memory.Update("", null));
    }

    [Fact]
    public void MarkAsInactive_Should_Set_IsActive_False()
    {
        var memory = UserMemory.Create(Guid.NewGuid(), "content", null);

        memory.MarkAsInactive();

        memory.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void MarkAsActive_Should_Set_IsActive_True()
    {
        var memory = UserMemory.Create(Guid.NewGuid(), "content", null);
        memory.MarkAsInactive();

        memory.MarkAsActive();

        memory.IsActive.ShouldBeTrue();
    }
}
