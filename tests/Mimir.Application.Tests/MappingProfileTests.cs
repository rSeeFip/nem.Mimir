using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using Shouldly;

namespace Mimir.Application.Tests;

public sealed class MappingProfileTests
{
    private readonly MimirMapper _mapper = new();

    [Fact]
    public void MapToUserDto_ShouldMapAllProperties()
    {
        // Arrange
        var user = User.Create("testuser", "test@example.com", UserRole.Admin);

        // Act
        var dto = _mapper.MapToUserDto(user);

        // Assert
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(user.Id);
        dto.Username.ShouldBe("testuser");
        dto.Email.ShouldBe("test@example.com");
        dto.Role.ShouldBe("Admin");
    }

    [Fact]
    public void MapToSystemPromptDto_ShouldMapAllProperties()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Test Prompt", "You are a helper.", "A test prompt");

        // Act
        var dto = _mapper.MapToSystemPromptDto(prompt);

        // Assert
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(prompt.Id);
        dto.Name.ShouldBe("Test Prompt");
        dto.Template.ShouldBe("You are a helper.");
    }
}
