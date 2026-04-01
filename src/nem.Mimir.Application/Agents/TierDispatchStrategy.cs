using System.Globalization;
using System.Text.RegularExpressions;
using nem.Contracts.Agents;
using nem.Contracts.Inference;
using ContractSpecialistAgent = nem.Contracts.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Agents;

public sealed partial class TierDispatchStrategy
{
    public InferenceTier ResolveEntryTier(AgentTask task, TierConfiguration configuration)
    {
        if (task.Context is not null
            && task.Context.TryGetValue("inferenceTier", out var configuredTier)
            && Enum.TryParse<InferenceTier>(configuredTier, ignoreCase: true, out var explicitTier))
        {
            return explicitTier;
        }

        return configuration.EntryTierByTaskType.TryGetValue(task.Type, out var mapped)
            ? mapped
            : InferenceTier.Processing;
    }

    public InferenceTier InferTierForAgentName(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return InferenceTier.Processing;
        }

        var normalized = agentName.Trim().ToLowerInvariant();

        if (normalized.Contains("router", StringComparison.Ordinal)
            || normalized.Contains("explore", StringComparison.Ordinal))
        {
            return InferenceTier.Router;
        }

        if (normalized.Contains("valid", StringComparison.Ordinal)
            || normalized.Contains("schema", StringComparison.Ordinal))
        {
            return InferenceTier.Validation;
        }

        if (normalized.Contains("analy", StringComparison.Ordinal)
            || normalized.Contains("reason", StringComparison.Ordinal))
        {
            return InferenceTier.Analysis;
        }

        if (normalized.Contains("escal", StringComparison.Ordinal)
            || normalized.Contains("human", StringComparison.Ordinal))
        {
            return InferenceTier.Escalation;
        }

        return InferenceTier.Processing;
    }

    public IReadOnlyList<ContractSpecialistAgent> SelectCandidatesForTier(
        IReadOnlyList<ContractSpecialistAgent> candidates,
        InferenceTier tier,
        TierConfiguration configuration)
    {
        var matches = candidates
            .Where(agent => ResolveTier(agent, configuration) == tier)
            .ToList();

        return matches;
    }

    public IReadOnlyDictionary<string, string> BuildTierContext(
        IReadOnlyDictionary<string, string>? existing,
        InferenceTier tier,
        string model)
    {
        var context = existing is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);

        context["inferenceTier"] = tier.ToString();
        context["inferenceModel"] = model;
        return context;
    }

    public double EstimateConfidence(AgentResult result)
    {
        if (TryParseConfidenceFromArtifacts(result.Artifacts, out var artifactConfidence))
        {
            return artifactConfidence;
        }

        if (TryParseConfidenceFromText(result.Output, out var outputConfidence))
        {
            return outputConfidence;
        }

        return result.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || result.Status.Equals("Success", StringComparison.OrdinalIgnoreCase)
            ? 0.75d
            : 0.0d;
    }

    private InferenceTier ResolveTier(ContractSpecialistAgent agent, TierConfiguration configuration)
    {
        if (configuration.AgentTierAssignments.TryGetValue(agent.Name, out var assigned))
        {
            return assigned;
        }

        return InferTierForAgentName(agent.Name);
    }

    private static bool TryParseConfidenceFromArtifacts(IReadOnlyList<string>? artifacts, out double confidence)
    {
        if (artifacts is not null)
        {
            foreach (var artifact in artifacts)
            {
                if (TryParseConfidenceFromText(artifact, out confidence))
                {
                    return true;
                }
            }
        }

        confidence = 0d;
        return false;
    }

    private static bool TryParseConfidenceFromText(string? text, out double confidence)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            var match = ConfidencePattern().Match(text);
            if (match.Success
                && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out confidence)
                && confidence >= 0d
                && confidence <= 1d)
            {
                return true;
            }
        }

        confidence = 0d;
        return false;
    }

    [GeneratedRegex(@"confidence\s*[:=]\s*(0(?:\.\d+)?|1(?:\.0+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex ConfidencePattern();
}
