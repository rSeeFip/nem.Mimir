using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace nem.Mimir.Domain.Tests.Entities;

public class SystemPromptTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_SystemPrompt()
    {
        // Arrange
        var name = "Default Assistant";
        var template = "You are {{assistant_name}}, helping {{user_name}}.";
        var description = "Default assistant prompt";

        // Act
        var prompt = SystemPrompt.Create(name, template, description);

        // Assert
        prompt.Id.ShouldNotBe(Guid.Empty);
        prompt.Name.ShouldBe(name);
        prompt.Template.ShouldBe(template);
        prompt.Description.ShouldBe(description);
        prompt.IsDefault.ShouldBeFalse();
        prompt.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_With_Empty_Name_Should_Throw()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            SystemPrompt.Create(string.Empty, "template", "desc"));
    }

    [Fact]
    public void Create_With_Null_Name_Should_Throw()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            SystemPrompt.Create(null!, "template", "desc"));
    }

    [Fact]
    public void Create_With_Whitespace_Name_Should_Throw()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            SystemPrompt.Create("   ", "template", "desc"));
    }

    [Fact]
    public void Create_With_Empty_Template_Should_Throw()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            SystemPrompt.Create("name", string.Empty, "desc"));
    }

    [Fact]
    public void Create_With_Null_Template_Should_Throw()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            SystemPrompt.Create("name", null!, "desc"));
    }

    [Fact]
    public void UpdateName_With_Valid_Name_Should_Update()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Old Name", "template", "desc");
        var newName = "New Name";

        // Act
        prompt.UpdateName(newName);

        // Assert
        prompt.Name.ShouldBe(newName);
    }

    [Fact]
    public void UpdateName_With_Empty_Name_Should_Throw()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Name", "template", "desc");

        // Act & Assert
        Should.Throw<ArgumentException>(() => prompt.UpdateName(string.Empty));
    }

    [Fact]
    public void UpdateTemplate_With_Valid_Template_Should_Update()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Name", "old template", "desc");
        var newTemplate = "new template with {{variable}}";

        // Act
        prompt.UpdateTemplate(newTemplate);

        // Assert
        prompt.Template.ShouldBe(newTemplate);
    }

    [Fact]
    public void UpdateTemplate_With_Empty_Template_Should_Throw()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Name", "template", "desc");

        // Act & Assert
        Should.Throw<ArgumentException>(() => prompt.UpdateTemplate(string.Empty));
    }

    [Fact]
    public void UpdateDescription_Should_Update()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Name", "template", "old desc");
        var newDescription = "new description";

        // Act
        prompt.UpdateDescription(newDescription);

        // Assert
        prompt.Description.ShouldBe(newDescription);
    }

    [Fact]
    public void SetAsDefault_Should_Set_IsDefault_True()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Name", "template", "desc");

        // Act
        prompt.SetAsDefault();

        // Assert
        prompt.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void UnsetDefault_Should_Set_IsDefault_False()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Name", "template", "desc");
        prompt.SetAsDefault();

        // Act
        prompt.UnsetDefault();

        // Assert
        prompt.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void Deactivate_Should_Set_IsActive_False()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Name", "template", "desc");

        // Act
        prompt.Deactivate();

        // Assert
        prompt.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Activate_Should_Set_IsActive_True()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Name", "template", "desc");
        prompt.Deactivate();

        // Act
        prompt.Activate();

        // Assert
        prompt.IsActive.ShouldBeTrue();
    }
}
