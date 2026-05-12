using Microsoft.Extensions.Logging;

namespace nem.Mimir.Application.Context;

public sealed class PromptCompressionService
{
    private static readonly HashSet<string> SafetyKeywords =
    [
        "safety", "policy", "policies", "guardrail", "guardrails", "never", "must", "required", "compliance", "secure", "security"
    ];

    private static readonly HashSet<string> StopWords =
    [
        "the", "and", "that", "this", "with", "from", "have", "were", "what", "when", "where", "which", "will",
        "would", "there", "their", "about", "into", "your", "you", "for", "are", "but", "not", "can", "has", "had"
    ];

    private readonly PromptCompressionOptions _options;
    private readonly ILogger<PromptCompressionService> _logger;
    private readonly global::nem.Contracts.TokenOptimization.IContextOptimizer _contextOptimizer;

    public PromptCompressionService(
        global::Microsoft.Extensions.Options.IOptions<PromptCompressionOptions> options,
        global::nem.Contracts.TokenOptimization.IContextOptimizer contextOptimizer,
        ILogger<PromptCompressionService> logger)
    {
        _options = options.Value ?? new PromptCompressionOptions();
        _contextOptimizer = contextOptimizer;
        _logger = logger;
    }

    public Task<string> CompressAsync(string prompt, string queryContext, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Task.FromResult(prompt);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var blocks = ParseBlocks(prompt);
        if (blocks.Count == 0)
        {
            return Task.FromResult(prompt);
        }

        var compressedTextByIndex = new Dictionary<int, string>();

        foreach (var block in blocks)
        {
            if (block.Kind == PromptBlockKind.User)
            {
                compressedTextByIndex[block.Index] = block.Text;
                continue;
            }

            if (block.Kind == PromptBlockKind.System)
            {
                compressedTextByIndex[block.Index] = _options.CompressSystemPrompts
                    ? CompressSystemBlock(block.Text)
                    : block.Text;
                continue;
            }

            if (block.Kind is PromptBlockKind.Other)
            {
                compressedTextByIndex[block.Index] = CompactWhitespace(block.Text);
            }
        }

        var queryTerms = Tokenize(queryContext)
            .Where(x => x.Length >= 3 && !StopWords.Contains(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ApplyFewShotSelection(blocks, queryTerms, compressedTextByIndex);
        ApplyToolSelection(blocks, queryTerms, compressedTextByIndex);

        var final = BuildPrompt(blocks, compressedTextByIndex);
        if (string.IsNullOrWhiteSpace(final))
        {
            return Task.FromResult(prompt);
        }

        var beforeTokens = _contextOptimizer.EstimateTokens(prompt);
        var afterTokens = _contextOptimizer.EstimateTokens(final);
        var ratio = beforeTokens <= 0 ? 1d : (double)afterTokens / beforeTokens;

        _logger.LogInformation(
            "Prompt compression complete. beforeTokens={BeforeTokens}, afterTokens={AfterTokens}, ratio={CompressionRatio:0.000}",
            beforeTokens,
            afterTokens,
            ratio);

        return Task.FromResult(final);
    }

    private void ApplyFewShotSelection(
        IReadOnlyList<PromptBlock> blocks,
        HashSet<string> queryTerms,
        IDictionary<int, string> compressedTextByIndex)
    {
        var fewShots = blocks
            .Where(x => x.Kind == PromptBlockKind.FewShot)
            .OrderBy(x => x.Index)
            .ToList();

        if (fewShots.Count == 0)
        {
            return;
        }

        if (!_options.PruneFewShotExamples)
        {
            foreach (var block in fewShots)
            {
                compressedTextByIndex[block.Index] = CompactWhitespace(block.Text);
            }

            return;
        }

        var maxFewShot = Math.Max(1, _options.MaxFewShotExamples);
        var selected = fewShots
            .Select(x => new ScoredBlock(x, ScoreRelevance(x.Text, queryTerms)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Block.Index)
            .Take(maxFewShot)
            .Select(x => x.Block.Index)
            .ToHashSet();

        foreach (var block in fewShots)
        {
            if (selected.Contains(block.Index))
            {
                compressedTextByIndex[block.Index] = CompactWhitespace(block.Text);
            }
        }
    }

    private void ApplyToolSelection(
        IReadOnlyList<PromptBlock> blocks,
        HashSet<string> queryTerms,
        IDictionary<int, string> compressedTextByIndex)
    {
        var tools = blocks
            .Where(x => x.Kind == PromptBlockKind.Tool)
            .OrderBy(x => x.Index)
            .ToList();

        if (tools.Count == 0)
        {
            return;
        }

        var threshold = Math.Clamp(_options.ToolDescriptionRelevanceThreshold, 0.0f, 1.0f);
        var candidates = tools
            .Select(x => new ScoredBlock(x, ScoreRelevance(x.Text, queryTerms)))
            .Where(x => x.Score >= threshold)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Block.Index)
            .ToList();

        var budget = Math.Max(1, _options.MaxToolDescriptionTokens);
        var used = 0;

        foreach (var candidate in candidates)
        {
            var compact = CompactWhitespace(candidate.Block.Text);
            var tokens = _contextOptimizer.EstimateTokens(compact);
            if (used + tokens > budget)
            {
                continue;
            }

            compressedTextByIndex[candidate.Block.Index] = compact;
            used += tokens;
        }
    }

    private static string BuildPrompt(IReadOnlyList<PromptBlock> original, IReadOnlyDictionary<int, string> compressedTextByIndex)
    {
        var ordered = original
            .OrderBy(x => x.Index)
            .Where(x => compressedTextByIndex.ContainsKey(x.Index))
            .Select(x => compressedTextByIndex[x.Index])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        return string.Join("\n\n", ordered);
    }

    private static string CompressSystemBlock(string block)
    {
        var lines = block
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (lines.Count <= 1)
        {
            return CompactWhitespace(block);
        }

        var merged = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var compact = CompactWhitespace(line);
            var key = NormalizeForDedup(compact);
            if (seen.Contains(key) && !IsSafetyDirective(compact))
            {
                continue;
            }

            seen.Add(key);
            merged.Add(CompressPhrasing(compact));
        }

        var safetyLines = lines
            .Select(CompactWhitespace)
            .Where(IsSafetyDirective)
            .ToList();

        foreach (var safetyLine in safetyLines)
        {
            if (!merged.Any(x => x.Contains(safetyLine, StringComparison.OrdinalIgnoreCase)))
            {
                merged.Add(safetyLine);
            }
        }

        return string.Join('\n', merged.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string CompressPhrasing(string line)
    {
        var value = line;
        value = value.Replace("please ensure that", "ensure", StringComparison.OrdinalIgnoreCase);
        value = value.Replace("in order to", "to", StringComparison.OrdinalIgnoreCase);
        value = value.Replace("it is important to", "", StringComparison.OrdinalIgnoreCase);
        value = value.Replace("you should", "", StringComparison.OrdinalIgnoreCase);
        value = value.Replace("you must", "must", StringComparison.OrdinalIgnoreCase);
        value = value.Replace("at all times", "", StringComparison.OrdinalIgnoreCase);
        value = CompactWhitespace(value);
        return value.TrimStart(',', '-', ':', ';').Trim();
    }

    private static string CompactWhitespace(string input)
    {
        var lines = input
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(line => string.Join(' ', line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .ToList();

        return string.Join('\n', lines);
    }

    private static bool IsSafetyDirective(string line)
    {
        var lower = line.ToLowerInvariant();
        return SafetyKeywords.Any(lower.Contains);
    }

    private static string NormalizeForDedup(string value)
    {
        return string.Join(' ', Tokenize(value));
    }

    private static double ScoreRelevance(string text, IReadOnlySet<string> queryTerms)
    {
        if (queryTerms.Count == 0)
        {
            return 0;
        }

        var terms = Tokenize(text)
            .Where(x => x.Length >= 3 && !StopWords.Contains(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (terms.Count == 0)
        {
            return 0;
        }

        var overlap = terms.Count(queryTerms.Contains);
        return Math.Clamp((double)overlap / terms.Count, 0, 1);
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        return text
            .ToLowerInvariant()
            .Split([' ', '\n', '\t', '.', ',', ':', ';', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '|', '/'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static List<PromptBlock> ParseBlocks(string prompt)
    {
        var normalized = prompt.Replace("\r\n", "\n", StringComparison.Ordinal);
        var parts = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return [];
        }

        return parts
            .Select((text, index) => new PromptBlock(index, text, Classify(text)))
            .ToList();
    }

    private static PromptBlockKind Classify(string block)
    {
        var value = block.TrimStart();
        if (value.StartsWith("user:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("role: user", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("[user]", StringComparison.OrdinalIgnoreCase))
        {
            return PromptBlockKind.User;
        }

        if (value.StartsWith("system:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("role: system", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("[system]", StringComparison.OrdinalIgnoreCase)
            || value.Contains("<system-reminder>", StringComparison.OrdinalIgnoreCase))
        {
            return PromptBlockKind.System;
        }

        if (value.StartsWith("few-shot", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("few shot", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("example:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("[example]", StringComparison.OrdinalIgnoreCase)
            || value.Contains("few-shot", StringComparison.OrdinalIgnoreCase))
        {
            return PromptBlockKind.FewShot;
        }

        if (value.StartsWith("tool:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("tools:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("[tool]", StringComparison.OrdinalIgnoreCase)
            || value.Contains("tool description", StringComparison.OrdinalIgnoreCase))
        {
            return PromptBlockKind.Tool;
        }

        return PromptBlockKind.Other;
    }

    private sealed record PromptBlock(int Index, string Text, PromptBlockKind Kind);

    private sealed record ScoredBlock(PromptBlock Block, double Score);

    private enum PromptBlockKind
    {
        Other,
        System,
        FewShot,
        Tool,
        User,
    }
}
