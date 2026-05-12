using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public sealed class ModelProfileTests
{
    [Fact]
    public void Create_WithValidValues_ShouldCreateProfile()
    {
        var userId = Guid.NewGuid();
        var parameters = new ModelParameters(
            Temperature: 0.7m,
            TopP: 0.95m,
            MaxTokens: 2048,
            FrequencyPenalty: 0.1m,
            PresencePenalty: 0.2m,
            StopSequences: ["END"],
            SystemPromptOverride: "You are concise.",
            ResponseFormat: "json");

        var profile = ModelProfile.Create(userId, "Creative", "gpt-4o", parameters);

        profile.Id.ShouldNotBe(ModelProfileId.Empty);
        profile.UserId.ShouldBe(userId);
        profile.Name.ShouldBe("Creative");
        profile.ModelId.ShouldBe("gpt-4o");
        profile.Parameters.Temperature.ShouldBe(0.7m);
        profile.Parameters.StopSequences.ShouldContain("END");
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            ModelProfile.Create(Guid.NewGuid(), " ", "gpt-4o", ModelParameters.Default));
    }

    [Fact]
    public void Update_ShouldReplaceModelAndParameters()
    {
        var profile = ModelProfile.Create(Guid.NewGuid(), "Default", "phi-4-mini", ModelParameters.Default);

        profile.Update(
            "Reasoning",
            "qwen2.5-72b",
            new ModelParameters(
                Temperature: 0.2m,
                TopP: 0.8m,
                MaxTokens: 4096,
                FrequencyPenalty: null,
                PresencePenalty: null,
                StopSequences: ["\n\n"],
                SystemPromptOverride: null,
                ResponseFormat: "text"));

        profile.Name.ShouldBe("Reasoning");
        profile.ModelId.ShouldBe("qwen2.5-72b");
        profile.Parameters.MaxTokens.ShouldBe(4096);
        profile.Parameters.ResponseFormat.ShouldBe("text");
    }
}
