using System.Text.Json.Serialization;

namespace nem.Mimir.Api.Models.OpenAi;

/// <summary>
/// OpenAI-compatible response for the GET /v1/models endpoint.
/// </summary>
public sealed record OpenAiModelsResponse
{
    /// <summary>Object type, always "list".</summary>
    [JsonPropertyName("object")]
    public string Object { get; init; } = "list";

    /// <summary>The list of available models.</summary>
    [JsonPropertyName("data")]
    public required List<OpenAiModel> Data { get; init; }
}

/// <summary>
/// An individual model entry in the OpenAI models list.
/// </summary>
public sealed record OpenAiModel
{
    /// <summary>The model identifier.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Object type, always "model".</summary>
    [JsonPropertyName("object")]
    public string Object { get; init; } = "model";

    /// <summary>Unix timestamp (seconds) when the model was created.</summary>
    [JsonPropertyName("created")]
    public long Created { get; init; }

    /// <summary>The organization that owns the model.</summary>
    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; init; } = "mimir";
}
