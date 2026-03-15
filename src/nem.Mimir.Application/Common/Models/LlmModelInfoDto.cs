namespace nem.Mimir.Application.Common.Models;

/// <summary>
/// DTO containing information about an available LLM model.
/// </summary>
/// <param name="Id">The unique identifier for the model (e.g., "phi-4-mini").</param>
/// <param name="Name">The display name of the model.</param>
/// <param name="ContextWindow">The maximum context window size in tokens.</param>
/// <param name="RecommendedUse">The recommended use case for the model.</param>
/// <param name="IsAvailable">Whether the model is currently available in the LiteLLM proxy.</param>
public sealed record LlmModelInfoDto(
    string Id,
    string Name,
    int ContextWindow,
    string RecommendedUse,
    bool IsAvailable);
