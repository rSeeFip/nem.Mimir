using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public class FolderTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateFolder()
    {
        var userId = Guid.NewGuid();

        var folder = Folder.Create(userId, "Work");

        folder.Id.ShouldNotBe(FolderId.Empty);
        folder.UserId.ShouldBe(userId);
        folder.Name.ShouldBe("Work");
        folder.ParentId.ShouldBeNull();
        folder.IsExpanded.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => Folder.Create(Guid.NewGuid(), " "));
    }

    [Fact]
    public void Rename_ShouldUpdateName()
    {
        var folder = Folder.Create(Guid.NewGuid(), "Old");

        folder.Rename("New");

        folder.Name.ShouldBe("New");
    }

    [Fact]
    public void SetParent_ShouldUpdateParentId()
    {
        var folder = Folder.Create(Guid.NewGuid(), "Work");
        var parentId = FolderId.New();

        folder.SetParent(parentId);

        folder.ParentId.ShouldBe(parentId);
    }

    [Fact]
    public void ToggleExpanded_ShouldToggleState()
    {
        var folder = Folder.Create(Guid.NewGuid(), "Work");

        folder.ToggleExpanded();
        folder.IsExpanded.ShouldBeTrue();

        folder.ToggleExpanded();
        folder.IsExpanded.ShouldBeFalse();
    }
}
