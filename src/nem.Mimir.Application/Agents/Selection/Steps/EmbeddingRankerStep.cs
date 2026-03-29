using System.Text.Json;
using Microsoft.Extensions.Logging;
using nem.Contracts.Identity;
using nem.Contracts.Inference;

namespace nem.Mimir.Application.Agents.Selection.Steps;

public sealed class EmbeddingRankerStep : ISelectionStep
{
    private const string EmbeddingScoreKey = "embedding";
    private readonly IInferenceGateway _inferenceGateway;
    private readonly ILogger<EmbeddingRankerStep> _logger;

    public EmbeddingRankerStep(IInferenceGateway inferenceGateway, ILogger<EmbeddingRankerStep> logger)
    {
        _inferenceGateway = inferenceGateway;
        _logger = logger;
    }

    public async Task<SelectionContext> ExecuteAsync(SelectionContext context, CancellationToken ct = default)
    {
        if (context.Candidates.Count == 0)
        {
            return context;
        }

        try
        {
            var taskEmbedding = await EmbedTextAsync(context.Task.Prompt, ct).ConfigureAwait(false);
            if (taskEmbedding is null)
            {
                _logger.LogWarning("EmbeddingRankerStep could not obtain task embedding; returning candidates unchanged.");
                return context;
            }

            var ranked = new List<ScoredAgent>(context.Candidates.Count);

            foreach (var candidate in context.Candidates)
            {
                ct.ThrowIfCancellationRequested();

                var embedding = await EmbedTextAsync(candidate.Agent.Description, ct).ConfigureAwait(false);
                if (embedding is null)
                {
                    _logger.LogWarning(
                        "EmbeddingRankerStep could not obtain embedding for agent {AgentName}; using score 0.",
                        candidate.Agent.Name);

                    ranked.Add(candidate.AddStepScore(EmbeddingScoreKey, 0));
                    continue;
                }

                var similarity = CosineSimilarity(taskEmbedding, embedding);
                ranked.Add(candidate.AddStepScore(EmbeddingScoreKey, similarity));
            }

            var ordered = ranked
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Agent.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return context.WithCandidates(ordered);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EmbeddingRankerStep failed to score candidates; returning context unchanged.");
            return context;
        }
    }

    private async Task<double[]?> EmbedTextAsync(string text, CancellationToken ct)
    {
        var request = new InferenceRequest(
            Alias: InferenceModelAlias.Embedding,
            Messages: [new InferenceMessage("user", text)],
            CorrelationId: CorrelationId.New());

        var response = await _inferenceGateway.SendAsync(request, ct).ConfigureAwait(false);
        return TryParseEmbedding(response.Content);
    }

    private static double[]? TryParseEmbedding(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => ParseArray(document.RootElement),
                JsonValueKind.Object => ParseObject(document.RootElement),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static double[]? ParseObject(JsonElement element)
    {
        if (element.TryGetProperty("embedding", out var embedding) && embedding.ValueKind == JsonValueKind.Array)
        {
            return ParseArray(embedding);
        }

        if (element.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array
            && data.GetArrayLength() > 0)
        {
            var first = data[0];
            if (first.ValueKind == JsonValueKind.Object
                && first.TryGetProperty("embedding", out var nestedEmbedding)
                && nestedEmbedding.ValueKind == JsonValueKind.Array)
            {
                return ParseArray(nestedEmbedding);
            }
        }

        return null;
    }

    private static double[]? ParseArray(JsonElement array)
    {
        var values = new double[array.GetArrayLength()];

        for (var i = 0; i < array.GetArrayLength(); i++)
        {
            var item = array[i];
            if (item.ValueKind is not (JsonValueKind.Number))
            {
                return null;
            }

            values[i] = item.GetDouble();
        }

        return values;
    }

    private static double CosineSimilarity(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        if (left.Count == 0 || right.Count == 0 || left.Count != right.Count)
        {
            return 0;
        }

        var dot = 0d;
        var leftNorm = 0d;
        var rightNorm = 0d;

        for (var i = 0; i < left.Count; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        if (leftNorm <= 0 || rightNorm <= 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }
}
