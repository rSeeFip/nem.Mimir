using System.Text.RegularExpressions;

namespace nem.Mimir.Application.Agents.Selection.Steps;

public sealed partial class Bm25RankerStep : ISelectionStep
{
    private const string Bm25ScoreKey = "bm25";
    private const double K1 = 1.2;
    private const double B = 0.75;

    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "has", "he", "in", "is", "it", "its",
        "of", "on", "that", "the", "to", "was", "were", "will", "with", "this", "those", "these", "or", "not", "but",
        "do", "does", "did", "can", "could", "should", "would", "you", "your", "we", "our", "they", "their", "i", "me", "my",
    ];

    [GeneratedRegex("[^\\p{L}\\p{N}]+", RegexOptions.Compiled)]
    private static partial Regex TokenSplitRegex();

    public Task<SelectionContext> ExecuteAsync(SelectionContext context, CancellationToken ct = default)
    {
        if (context.Candidates.Count == 0)
        {
            return Task.FromResult(context);
        }

        var queryTokens = Tokenize(context.Task.Prompt);
        if (queryTokens.Count == 0)
        {
            var passthrough = context.Candidates
                .Select(candidate => candidate.AddStepScore(Bm25ScoreKey, 0))
                .ToList();

            return Task.FromResult(context.WithCandidates(passthrough));
        }

        var documents = context.Candidates
            .Select(candidate => new Document(candidate, Tokenize(BuildAgentDocument(candidate))))
            .ToList();

        var averageDocumentLength = documents.Count == 0
            ? 0
            : documents.Average(d => d.Tokens.Count);

        var documentFrequency = BuildDocumentFrequency(documents, queryTokens);

        var ranked = documents
            .Select(document => document.Candidate.AddStepScore(
                Bm25ScoreKey,
                ScoreDocument(document, queryTokens, documentFrequency, documents.Count, averageDocumentLength)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Agent.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(context.WithCandidates(ranked));
    }

    private static double ScoreDocument(
        Document document,
        IReadOnlyList<string> queryTokens,
        IReadOnlyDictionary<string, int> documentFrequency,
        int documentCount,
        double averageDocumentLength)
    {
        if (document.Tokens.Count == 0 || averageDocumentLength <= 0)
        {
            return 0;
        }

        var termFrequency = document.Tokens
            .GroupBy(token => token, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var score = 0d;

        foreach (var token in queryTokens)
        {
            if (!termFrequency.TryGetValue(token, out var frequency) || frequency == 0)
            {
                continue;
            }

            var df = documentFrequency.TryGetValue(token, out var value) ? value : 0;
            var idf = Math.Log(1 + ((documentCount - df + 0.5) / (df + 0.5)));

            var normalization = frequency + (K1 * (1 - B + (B * document.Tokens.Count / averageDocumentLength)));
            var weightedFrequency = (frequency * (K1 + 1)) / normalization;

            score += idf * weightedFrequency;
        }

        return score;
    }

    private static Dictionary<string, int> BuildDocumentFrequency(IReadOnlyList<Document> documents, IReadOnlyList<string> queryTokens)
    {
        var queryTokenSet = new HashSet<string>(queryTokens, StringComparer.Ordinal);
        var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var token in queryTokenSet)
        {
            var count = 0;

            foreach (var document in documents)
            {
                if (document.Tokens.Contains(token, StringComparer.Ordinal))
                {
                    count++;
                }
            }

            documentFrequency[token] = count;
        }

        return documentFrequency;
    }

    private static List<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return TokenSplitRegex()
            .Split(text.ToLowerInvariant())
            .Where(token => token.Length > 0 && !StopWords.Contains(token))
            .ToList();
    }

    private static string BuildAgentDocument(ScoredAgent candidate)
    {
        var capabilityText = string.Join(' ', candidate.Agent.Capabilities.Select(static capability => capability.ToString()));
        return $"{candidate.Agent.Name} {candidate.Agent.Description} {capabilityText}";
    }

    private sealed record Document(ScoredAgent Candidate, IReadOnlyList<string> Tokens);
}
