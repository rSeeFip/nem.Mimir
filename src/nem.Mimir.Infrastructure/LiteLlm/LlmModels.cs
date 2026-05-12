namespace nem.Mimir.Infrastructure.LiteLlm;

/// <summary>
/// Available LLM model identifiers routed through LiteLLM proxy.
/// </summary>
public static class LlmModels
{
    /// <summary>Fast responses (&gt;40 tok/s) — Phi-4 Mini.</summary>
    public const string Fast = "phi-4-mini";

    /// <summary>Primary reasoning model — Qwen 2.5 72B.</summary>
    public const string Primary = "qwen-2.5-72b";

    /// <summary>Code generation model — Qwen 2.5 Coder 32B.</summary>
    public const string Coding = "qwen-2.5-coder-32b";

    /// <summary>Embedding model — Nomic Embed Text.</summary>
    public const string Embedding = "nomic-embed-text";

    /// <summary>Default model used when none specified.</summary>
    public const string Default = Primary;

    /// <summary>
    /// Approximate token count from text using character-based heuristic.
    /// Used for context window management, not billing.
    /// </summary>
    public static int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}
