using System.Globalization;
using System.Text.RegularExpressions;
using nem.Contracts.Agents;
using nem.Contracts.Inference;
using nem.Mimir.Application.Common.Interfaces;
using ContractSpecialistAgent = nem.Contracts.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Agents;

public sealed partial class TierDispatchStrategy
{
    public InferenceTier ResolveEntryTier(AgentTask task, IOrchestrationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.ResolveEntryTier(task);
    }

    public InferenceTier InferTierForAgentName(string agentName, IOrchestrationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.ResolveTierForAgent(agentName);
    }

    public IReadOnlyList<ContractSpecialistAgent> SelectCandidatesForTier(
        IReadOnlyList<ContractSpecialistAgent> candidates,
        InferenceTier tier,
        IOrchestrationPlan plan)
    {
        var matches = candidates
            .Where(agent => ResolveTier(agent, plan) == tier)
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

    private InferenceTier ResolveTier(ContractSpecialistAgent agent, IOrchestrationPlan plan)
    {
        return plan.ResolveTierForAgent(agent.Name);
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
