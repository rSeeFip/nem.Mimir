namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.ValueObjects;

public sealed class ModelProfile : BaseAuditableEntity<ModelProfileId>
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ModelId { get; private set; } = string.Empty;
    public ModelParameters Parameters { get; private set; } = ModelParameters.Default;

    private ModelProfile() { }

    public static ModelProfile Create(
        Guid userId,
        string name,
        string modelId,
        ModelParameters parameters)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model ID cannot be empty.", nameof(modelId));

        return new ModelProfile
        {
            Id = ModelProfileId.New(),
            UserId = userId,
            Name = name.Trim(),
            ModelId = modelId.Trim(),
            Parameters = parameters,
        };
    }

    public void Update(
        string name,
        string modelId,
        ModelParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model ID cannot be empty.", nameof(modelId));

        Name = name.Trim();
        ModelId = modelId.Trim();
        Parameters = parameters;
    }
}

public sealed record ModelParameters(
    decimal? Temperature,
    decimal? TopP,
    int? MaxTokens,
    decimal? FrequencyPenalty,
    decimal? PresencePenalty,
    IReadOnlyList<string> StopSequences,
    string? SystemPromptOverride,
    string? ResponseFormat)
{
    public static readonly ModelParameters Default = new(
        Temperature: null,
        TopP: null,
        MaxTokens: null,
        FrequencyPenalty: null,
        PresencePenalty: null,
        StopSequences: Array.Empty<string>(),
        SystemPromptOverride: null,
        ResponseFormat: null);
}
