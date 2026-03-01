namespace Mimir.Domain.ValueObjects;

using Mimir.Domain.Common;

public sealed class ConversationSettings : ValueObject
{
    public int MaxTokens { get; }
    public double Temperature { get; }
    public string Model { get; }
    public int AutoArchiveAfterDays { get; }
    public SystemPromptId? SystemPromptId { get; }

    #pragma warning disable CS8618 // Required by EF Core
    private ConversationSettings() { }
#pragma warning restore CS8618

    public ConversationSettings(
        int maxTokens,
        double temperature,
        string model,
        int autoArchiveAfterDays,
        SystemPromptId? systemPromptId)
    {
        if (maxTokens < 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Max tokens cannot be negative.");

        if (temperature < 0.0 || temperature > 2.0)
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be between 0.0 and 2.0.");

        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model cannot be empty.", nameof(model));

        if (autoArchiveAfterDays < 0)
            throw new ArgumentOutOfRangeException(nameof(autoArchiveAfterDays), "Auto-archive days cannot be negative.");

        MaxTokens = maxTokens;
        Temperature = temperature;
        Model = model;
        AutoArchiveAfterDays = autoArchiveAfterDays;
        SystemPromptId = systemPromptId;
    }

    public static ConversationSettings Default => new(
        maxTokens: 4096,
        temperature: 0.7,
        model: "gpt-4",
        autoArchiveAfterDays: 30,
        systemPromptId: null);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MaxTokens;
        yield return Temperature;
        yield return Model;
        yield return AutoArchiveAfterDays;
        yield return SystemPromptId;
    }
}
