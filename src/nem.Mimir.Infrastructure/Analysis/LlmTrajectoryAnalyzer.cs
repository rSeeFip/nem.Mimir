namespace nem.Mimir.Infrastructure.Analysis;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using nem.Contracts.Events.Integration;
using nem.Contracts.Identity;
using nem.Contracts.Inference;
using nem.Mimir.Application.Analysis;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;
using nem.Mimir.Infrastructure.Observability;
using nem.Mimir.Infrastructure.Persistence;

internal sealed class LlmTrajectoryAnalyzer(MimirDbContext dbContext, IInferenceGateway inferenceGateway) : ITrajectoryAnalyzer
{
    private const int PromptFieldMaxLength = 600;

    public async Task<EvolutionSuggestedEvent?> AnalyzeAsync(global::nem.Contracts.Identity.TrajectoryId trajectoryId, CancellationToken ct = default)
    {
        var trajectory = await dbContext.ExecutionTrajectories
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trajectoryId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Execution trajectory '{trajectoryId}' was not found.");

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(trajectory);

        var request = new InferenceRequest(
            Alias: InferenceModelAlias.Standard,
            Messages: new List<InferenceMessage>
            {
                new("system", systemPrompt),
                new("user", userPrompt)
            },
            CorrelationId: CorrelationId.New(),
            MaxTokens: 2000,
            Temperature: 0.3);

        using var activity = TrajectoryTracer.ActivitySource.StartActivity("trajectory.analyze.llm", ActivityKind.Client);
        activity?.SetTag("trajectory.id", trajectoryId.ToString());

        var startedAt = Stopwatch.GetTimestamp();
        var response = await inferenceGateway.SendAsync(request, ct).ConfigureAwait(false);
        var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        TrajectoryTracer.AnalysisLatencyMs.Record(elapsedMs);

        var parsed = ParseResponse(response.Content);

        if (parsed.Action is null)
        {
            return null;
        }

        return new EvolutionSuggestedEvent(
            AnalysisResultId.New(),
            SkillId.Empty,
            trajectory.Id,
            parsed.Action,
            parsed.Rationale,
            DateTimeOffset.UtcNow);
    }

    private static string BuildSystemPrompt() =>
        """
        You are a post-execution trajectory analyzer for an AI agent system.

        Analyze the provided execution trajectory steps and identify failures, inefficiencies, or missing capabilities.

        Action classification rules:
        - FIX: Existing skill appears broken or unreliable and needs correction.
        - DERIVE: Existing skill generally works but needs enhancement, optimization, or extension.
        - CAPTURE: No existing skill covers the needed capability and a new skill should be created.
        - NONE: No meaningful issue found.

        Respond ONLY in JSON with this exact shape:
        {
          "action": "FIX|DERIVE|CAPTURE|NONE",
          "rationale": "short explanation"
        }

        If uncertain, choose NONE.
        """;

    private static string BuildUserPrompt(ExecutionTrajectory trajectory)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"SessionId: {trajectory.SessionId}");
        sb.AppendLine($"AgentId: {trajectory.AgentId}");
        sb.AppendLine($"Succeeded: {trajectory.IsSuccess}");
        sb.AppendLine($"ErrorMessage: {trajectory.ErrorMessage ?? "<none>"}");
        sb.AppendLine($"StartedAt: {trajectory.StartedAt:O}");
        sb.AppendLine($"CompletedAt: {(trajectory.CompletedAt.HasValue ? trajectory.CompletedAt.Value.ToString("O") : "<none>")}");
        sb.AppendLine($"TotalSteps: {trajectory.TotalSteps}");
        sb.AppendLine();
        sb.AppendLine("Steps:");

        if (trajectory.Steps.Count == 0)
        {
            sb.AppendLine("- <no steps recorded>");
            return sb.ToString();
        }

        for (var i = 0; i < trajectory.Steps.Count; i++)
        {
            var step = trajectory.Steps[i];
            AppendStep(sb, i + 1, step);
        }

        return sb.ToString();
    }

    private static void AppendStep(StringBuilder sb, int index, TrajectoryStep step)
    {
        sb.AppendLine($"Step {index}:");
        sb.AppendLine($"  Action: {step.Action}");
        sb.AppendLine($"  Input: {Truncate(step.Input, PromptFieldMaxLength)}");
        sb.AppendLine($"  Output: {Truncate(step.Output, PromptFieldMaxLength)}");
        sb.AppendLine($"  Duration: {step.Duration}");
        sb.AppendLine($"  IsSuccess: {step.IsSuccess}");
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<none>";
        }

        return value.Length <= maxLength
            ? value
            : $"{value[..maxLength]}...(truncated)";
    }

    private static ParsedAnalysis ParseResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ParsedAnalysis.None;
        }

        var jsonPayload = ExtractJsonObject(content);
        if (!string.IsNullOrWhiteSpace(jsonPayload))
        {
            try
            {
                using var document = JsonDocument.Parse(jsonPayload);
                var root = document.RootElement;

                var action = root.TryGetProperty("action", out var actionElement)
                    ? NormalizeAction(actionElement.GetString())
                    : null;

                var rationale = root.TryGetProperty("rationale", out var rationaleElement)
                    ? rationaleElement.GetString()
                    : null;

                if (action is null)
                {
                    return ParsedAnalysis.None;
                }

                return new ParsedAnalysis(action, string.IsNullOrWhiteSpace(rationale) ? "LLM indicated trajectory issue." : rationale.Trim());
            }
            catch (JsonException)
            {
                // Fallback to line-based parsing below.
            }
        }

        return ParseLineBased(content);
    }

    private static ParsedAnalysis ParseLineBased(string content)
    {
        string? action = null;
        string? rationale = null;

        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("action", StringComparison.OrdinalIgnoreCase))
            {
                var split = line.Split(':', 2);
                if (split.Length == 2)
                {
                    action = NormalizeAction(split[1]);
                }
            }

            if (line.StartsWith("rationale", StringComparison.OrdinalIgnoreCase))
            {
                var split = line.Split(':', 2);
                if (split.Length == 2)
                {
                    rationale = split[1].Trim();
                }
            }
        }

        if (action is null)
        {
            return ParsedAnalysis.None;
        }

        return new ParsedAnalysis(action, string.IsNullOrWhiteSpace(rationale) ? "LLM indicated trajectory issue." : rationale);
    }

    private static string? NormalizeAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return null;
        }

        var normalized = action.Trim().Trim('"').ToUpperInvariant();

        return normalized switch
        {
            "FIX" => "FIX",
            "DERIVE" => "DERIVE",
            "CAPTURE" => "CAPTURE",
            "NONE" => null,
            _ => null,
        };
    }

    private static string? ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed[firstBrace..(lastBrace + 1)];
            }
        }

        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return null;
    }

    private sealed record ParsedAnalysis(string? Action, string Rationale)
    {
        public static readonly ParsedAnalysis None = new(null, string.Empty);
    }
}
