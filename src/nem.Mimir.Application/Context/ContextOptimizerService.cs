using System.Text;
using Microsoft.Extensions.Logging;
using nem.Contracts.TokenOptimization;

namespace nem.Mimir.Application.Context;

/// <summary>
/// Optimizes context content through distillation, pruning, and compression to reduce token usage.
/// </summary>
public sealed class ContextOptimizerService : IContextOptimizer
{
    private static readonly HashSet<string> StopWords =
    [
        "the", "and", "that", "this", "with", "from", "have", "were", "what", "when", "where", "which", "will",
        "would", "there", "their", "about", "into", "your", "you", "for", "are", "but", "not", "can", "has", "had"
    ];

    private readonly ContextOptimizerOptions _options;
    private readonly ILogger<ContextOptimizerService> _logger;

    public ContextOptimizerService(ContextOptimizerOptions options, ILogger<ContextOptimizerService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<string> DistillAsync(string content, int targetTokens, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(content);
        }

        if (targetTokens <= 0)
        {
            return Task.FromResult(content);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentTokens = EstimateTokens(content);
            if (currentTokens <= targetTokens)
            {
                return Task.FromResult(content);
            }

            var blocks = ParseBlocks(content);
            if (blocks.Count == 0)
            {
                return Task.FromResult(content);
            }

            MarkProtectedBlocks(blocks);
            ScoreBlocks(blocks);

            var kept = BuildDistilledSet(blocks, targetTokens);
            var distilled = RebuildContent(kept.OrderBy(x => x.Index));

            if (string.IsNullOrWhiteSpace(distilled))
            {
                return Task.FromResult(content);
            }

            return Task.FromResult(distilled);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distillation failed. Returning original content.");
            return Task.FromResult(content);
        }
    }

    public async Task<string> PruneAsync(string content, PruneStrategy strategy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tokens = EstimateTokens(content);
            if (!ShouldAutoTrigger(tokens))
            {
                return content;
            }

            var safeRatio = Math.Clamp(_options.TargetCompressionRatio, 0.4d, 0.6d);
            var targetTokens = Math.Max(1, (int)Math.Floor(tokens * safeRatio));

            return strategy switch
            {
                PruneStrategy.RemoveOldest => await RemoveOldestAsync(content, targetTokens, cancellationToken).ConfigureAwait(false),
                PruneStrategy.RemoveLeastRelevant => await RemoveLeastRelevantAsync(content, targetTokens, cancellationToken).ConfigureAwait(false),
                PruneStrategy.Summarize => await DistillAsync(content, targetTokens, cancellationToken).ConfigureAwait(false),
                PruneStrategy.Truncate => TruncateContent(content, targetTokens),
                _ => content,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pruning failed. Returning original content.");
            return content;
        }
    }

    public int EstimateTokens(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0;
        }

        var charsPerToken = _options.CharsPerToken <= 0 ? 4.0d : _options.CharsPerToken;
        return Math.Max(1, (int)Math.Ceiling(content.Length / charsPerToken));
    }

    public Task<string> CompressAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(content);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tokens = EstimateTokens(content);
            if (!ShouldAutoTrigger(tokens))
            {
                return Task.FromResult(content);
            }

            var blocks = ParseBlocks(content);
            if (blocks.Count == 0)
            {
                return Task.FromResult(content);
            }

            MarkProtectedBlocks(blocks);

            foreach (var block in blocks)
            {
                if (block.IsProtected)
                {
                    continue;
                }

                block.Text = CompactWhitespace(block.Text);
            }

            var compressed = RebuildContent(blocks.OrderBy(x => x.Index));
            return Task.FromResult(string.IsNullOrWhiteSpace(compressed) ? content : compressed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Compression failed. Returning original content.");
            return Task.FromResult(content);
        }
    }

    private async Task<string> RemoveOldestAsync(string content, int targetTokens, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var blocks = ParseBlocks(content);
        if (blocks.Count == 0)
        {
            return content;
        }

        MarkProtectedBlocks(blocks);
        var kept = blocks.ToList();

        foreach (var block in blocks.OrderBy(x => x.Index))
        {
            if (EstimateTokens(RebuildContent(kept.OrderBy(x => x.Index))) <= targetTokens)
            {
                break;
            }

            if (block.IsProtected)
            {
                continue;
            }

            kept.Remove(block);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        var result = RebuildContent(kept.OrderBy(x => x.Index));
        return string.IsNullOrWhiteSpace(result) ? content : result;
    }

    private async Task<string> RemoveLeastRelevantAsync(string content, int targetTokens, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var blocks = ParseBlocks(content);
        if (blocks.Count == 0)
        {
            return content;
        }

        MarkProtectedBlocks(blocks);
        ScoreBlocks(blocks);
        var kept = blocks.ToList();

        foreach (var block in blocks
                     .Where(x => !x.IsProtected)
                     .OrderBy(x => x.Priority)
                     .ThenBy(x => x.Index))
        {
            if (EstimateTokens(RebuildContent(kept.OrderBy(x => x.Index))) <= targetTokens)
            {
                break;
            }

            kept.Remove(block);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        var result = RebuildContent(kept.OrderBy(x => x.Index));
        return string.IsNullOrWhiteSpace(result) ? content : result;
    }

    private string TruncateContent(string content, int targetTokens)
    {
        var blocks = ParseBlocks(content);
        if (blocks.Count == 0)
        {
            return content;
        }

        MarkProtectedBlocks(blocks);
        var protectedBlocks = blocks.Where(x => x.IsProtected).OrderBy(x => x.Index).ToList();
        var protectedContent = RebuildContent(protectedBlocks);

        if (EstimateTokens(protectedContent) >= targetTokens)
        {
            return protectedContent;
        }

        var remaining = targetTokens - EstimateTokens(protectedContent);
        var nonProtected = blocks.Where(x => !x.IsProtected).OrderBy(x => x.Index).ToList();

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(protectedContent))
        {
            builder.AppendLine(protectedContent);
            builder.AppendLine();
        }

        foreach (var block in nonProtected)
        {
            if (remaining <= 0)
            {
                break;
            }

            var blockTokens = EstimateTokens(block.Text);
            if (blockTokens <= remaining)
            {
                builder.AppendLine(block.Text);
                builder.AppendLine();
                remaining -= blockTokens;
                continue;
            }

            var allowedChars = Math.Max(8, (int)Math.Floor(remaining * (_options.CharsPerToken <= 0 ? 4.0d : _options.CharsPerToken)));
            var slice = block.Text.Length <= allowedChars
                ? block.Text
                : $"{block.Text[..allowedChars].TrimEnd()}...";
            builder.AppendLine(slice);
            break;
        }

        var result = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? content : result;
    }

    private bool ShouldAutoTrigger(int currentTokens)
    {
        if (_options.MaxContextTokens <= 0)
        {
            return true;
        }

        var threshold = Math.Clamp(_options.AutoTriggerThreshold, 0.1d, 1.0d);
        var triggerTokens = (int)Math.Ceiling(_options.MaxContextTokens * threshold);
        return currentTokens >= triggerTokens;
    }

    private static List<ContentBlock> ParseBlocks(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var split = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (split.Length == 0)
        {
            return [];
        }

        return split
            .Select((text, index) => new ContentBlock(index, text))
            .ToList();
    }

    private static void MarkProtectedBlocks(IReadOnlyList<ContentBlock> blocks)
    {
        if (blocks.Count == 0)
        {
            return;
        }

        var protectStart = Math.Max(0, blocks.Count - 3);
        for (var i = protectStart; i < blocks.Count; i++)
        {
            blocks[i].IsProtected = true;
        }

        var lastUser = blocks
            .Where(x => LooksLikeUserMessage(x.Text))
            .OrderByDescending(x => x.Index)
            .FirstOrDefault();

        if (lastUser is not null)
        {
            lastUser.IsProtected = true;
        }

        foreach (var block in blocks)
        {
            if (LooksLikeSystemPrompt(block.Text))
            {
                block.IsProtected = true;
            }
        }
    }

    private static void ScoreBlocks(IReadOnlyList<ContentBlock> blocks)
    {
        if (blocks.Count == 0)
        {
            return;
        }

        var corpus = string.Join(' ', blocks.Select(x => x.Text));
        var weights = BuildTermWeights(corpus);
        var count = blocks.Count;

        foreach (var block in blocks)
        {
            var recencyWeight = count <= 1 ? 1d : (double)(block.Index + 1) / count;
            var relevanceWeight = CalculateRelevanceScore(block.Text, weights);
            block.Priority = (0.45d * recencyWeight) + (0.55d * relevanceWeight);
        }
    }

    private static List<ContentBlock> BuildDistilledSet(IReadOnlyList<ContentBlock> blocks, int targetTokens)
    {
        var kept = blocks.Where(x => x.IsProtected).ToList();
        var keptTokenCount = EstimateTokens(kept);
        if (keptTokenCount >= targetTokens)
        {
            return kept;
        }

        foreach (var candidate in blocks
                     .Where(x => !x.IsProtected)
                     .OrderByDescending(x => x.Priority)
                     .ThenByDescending(x => x.Index))
        {
            var next = keptTokenCount + Math.Max(1, (int)Math.Ceiling(candidate.Text.Length / 4d));
            if (next > targetTokens)
            {
                continue;
            }

            kept.Add(candidate);
            keptTokenCount = next;

            if (keptTokenCount >= targetTokens)
            {
                break;
            }
        }

        return kept;

        static int EstimateTokens(IReadOnlyCollection<ContentBlock> collection)
            => Math.Max(0, collection.Sum(x => Math.Max(1, (int)Math.Ceiling(x.Text.Length / 4d))));
    }

    private static Dictionary<string, int> BuildTermWeights(string corpus)
    {
        var terms = Tokenize(corpus)
            .Where(x => x.Length >= 4 && !StopWords.Contains(x))
            .ToList();

        if (terms.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in terms)
        {
            map.TryGetValue(term, out var current);
            map[term] = current + 1;
        }

        return map;
    }

    private static double CalculateRelevanceScore(string text, IReadOnlyDictionary<string, int> weights)
    {
        if (weights.Count == 0)
        {
            return 0.5d;
        }

        var uniqueTerms = Tokenize(text)
            .Where(x => x.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniqueTerms.Count == 0)
        {
            return 0.1d;
        }

        double total = 0;
        foreach (var term in uniqueTerms)
        {
            if (weights.TryGetValue(term, out var weight))
            {
                total += weight;
            }
        }

        var max = weights.Values.Sum();
        if (max <= 0)
        {
            return 0.5d;
        }

        return Math.Clamp(total / max, 0.0d, 1.0d);
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        return text
            .ToLowerInvariant()
            .Split([' ', '\n', '\t', '.', ',', ':', ';', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '|', '/'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string CompactWhitespace(string input)
    {
        var lines = input
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(line => string.Join(' ', line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return string.Join('\n', lines);
    }

    private static string RebuildContent(IEnumerable<ContentBlock> blocks)
    {
        return string.Join("\n\n", blocks.Select(x => x.Text.Trim())).Trim();
    }

    private static bool LooksLikeSystemPrompt(string block)
    {
        var value = block.TrimStart();
        return value.StartsWith("system:", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("[system]", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("role: system", StringComparison.OrdinalIgnoreCase)
               || value.Contains("<system-reminder>", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeUserMessage(string block)
    {
        var value = block.TrimStart();
        return value.StartsWith("user:", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("[user]", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("role: user", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ContentBlock
    {
        public ContentBlock(int index, string text)
        {
            Index = index;
            Text = text;
        }

        public int Index { get; }

        public string Text { get; set; }

        public bool IsProtected { get; set; }

        public double Priority { get; set; }
    }
}
