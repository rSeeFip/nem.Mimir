namespace nem.Mimir.Infrastructure.Inference;

using nem.Contracts.Identity;
using nem.Contracts.Inference;
using nem.Contracts.TokenOptimization;

internal sealed class DefaultModelResolution : IModelResolution
{
    public Task<ResolvedModel> ResolveAsync(InferenceModelAlias alias, PolicyContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tier = InferenceGatewayService.ResolveAliasToTier(alias);
        var modelId = alias.Value.ToLowerInvariant() switch
        {
            "fast" => "phi-4-mini",
            "standard" => "qwen-2.5-72b",
            "premium" => "qwen-2.5-72b",
            "expert" => "qwen-2.5-72b",
            "coding" => "qwen-2.5-coder-32b",
            "embedding" => "nomic-embed-text",
            _ => alias.Value,
        };

        return Task.FromResult(new ResolvedModel(
            Alias: alias,
            ProviderId: InferenceProviderId.From(CreateDeterministicGuid("LiteLLM")),
            ModelId: modelId,
            Provider: "LiteLLM",
            Tier: tier,
            MaxContextWindow: 131072));
    }

    private static Guid CreateDeterministicGuid(string input)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash.AsSpan(0, 16));
    }
}
