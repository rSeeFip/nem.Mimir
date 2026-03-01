using Mimir.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Mimir.Domain.Tests.ValueObjects;

public class ConversationSettingsTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_ConversationSettings()
    {
        // Arrange & Act
        var settings = new ConversationSettings(
            maxTokens: 4096,
            temperature: 0.7,
            model: "gpt-4",
            autoArchiveAfterDays: 30,
            systemPromptId: null);

        // Assert
        settings.MaxTokens.ShouldBe(4096);
        settings.Temperature.ShouldBe(0.7);
        settings.Model.ShouldBe("gpt-4");
        settings.AutoArchiveAfterDays.ShouldBe(30);
        settings.SystemPromptId.ShouldBeNull();
    }

    [Fact]
    public void Create_With_SystemPromptId_Should_Store_It()
    {
        // Arrange
        var promptId = SystemPromptId.New();

        // Act
        var settings = new ConversationSettings(
            maxTokens: 2048,
            temperature: 0.5,
            model: "gpt-3.5-turbo",
            autoArchiveAfterDays: 0,
            systemPromptId: promptId);

        // Assert
        settings.SystemPromptId.ShouldNotBeNull();
        settings.SystemPromptId.Value.ShouldBe(promptId);
    }

    [Fact]
    public void Create_With_Zero_AutoArchive_Should_Mean_Never()
    {
        // Arrange & Act
        var settings = new ConversationSettings(
            maxTokens: 4096,
            temperature: 0.7,
            model: "gpt-4",
            autoArchiveAfterDays: 0,
            systemPromptId: null);

        // Assert
        settings.AutoArchiveAfterDays.ShouldBe(0);
    }

    [Fact]
    public void Create_With_Negative_MaxTokens_Should_Throw()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new ConversationSettings(
                maxTokens: -1,
                temperature: 0.7,
                model: "gpt-4",
                autoArchiveAfterDays: 0,
                systemPromptId: null));
    }

    [Fact]
    public void Create_With_Temperature_Below_Zero_Should_Throw()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new ConversationSettings(
                maxTokens: 4096,
                temperature: -0.1,
                model: "gpt-4",
                autoArchiveAfterDays: 0,
                systemPromptId: null));
    }

    [Fact]
    public void Create_With_Temperature_Above_Two_Should_Throw()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new ConversationSettings(
                maxTokens: 4096,
                temperature: 2.1,
                model: "gpt-4",
                autoArchiveAfterDays: 0,
                systemPromptId: null));
    }

    [Fact]
    public void Create_With_Negative_AutoArchiveDays_Should_Throw()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new ConversationSettings(
                maxTokens: 4096,
                temperature: 0.7,
                model: "gpt-4",
                autoArchiveAfterDays: -1,
                systemPromptId: null));
    }

    [Fact]
    public void Create_With_Empty_Model_Should_Throw()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            new ConversationSettings(
                maxTokens: 4096,
                temperature: 0.7,
                model: string.Empty,
                autoArchiveAfterDays: 0,
                systemPromptId: null));
    }

    [Fact]
    public void ConversationSettings_With_Same_Values_Should_Be_Equal()
    {
        // Arrange
        var settings1 = new ConversationSettings(4096, 0.7, "gpt-4", 30, null);
        var settings2 = new ConversationSettings(4096, 0.7, "gpt-4", 30, null);

        // Act & Assert
        settings1.ShouldBe(settings2);
    }

    [Fact]
    public void ConversationSettings_With_Different_Values_Should_Not_Be_Equal()
    {
        // Arrange
        var settings1 = new ConversationSettings(4096, 0.7, "gpt-4", 30, null);
        var settings2 = new ConversationSettings(2048, 0.7, "gpt-4", 30, null);

        // Act & Assert
        settings1.ShouldNotBe(settings2);
    }

    [Fact]
    public void Default_Should_Create_Sensible_Defaults()
    {
        // Act
        var settings = ConversationSettings.Default;

        // Assert
        settings.MaxTokens.ShouldBeGreaterThan(0);
        settings.Temperature.ShouldBeInRange(0.0, 2.0);
        settings.Model.ShouldNotBeNullOrWhiteSpace();
        settings.AutoArchiveAfterDays.ShouldBeGreaterThanOrEqualTo(0);
    }
}
