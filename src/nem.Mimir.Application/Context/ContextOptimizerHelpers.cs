namespace nem.Mimir.Application.Context;

internal static class ContextOptimizerHelpers
{
    private static readonly HashSet<string> StopWords =
    [
        "the", "and", "that", "this", "with", "from", "have", "were", "what", "when", "where", "which", "will",
        "would", "there", "their", "about", "into", "your", "you", "for", "are", "but", "not", "can", "has", "had"
    ];

    internal static List<ContentBlock> ParseBlocks(string content)
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

    internal static void MarkProtectedBlocks(IReadOnlyList<ContentBlock> blocks)
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

    internal static void ScoreBlocks(IReadOnlyList<ContentBlock> blocks)
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

    internal static List<ContentBlock> BuildDistilledSet(IReadOnlyList<ContentBlock> blocks, int targetTokens, double charsPerToken)
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
            var next = keptTokenCount + Math.Max(1, (int)Math.Ceiling(candidate.Text.Length / charsPerToken));
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

        int EstimateTokens(IReadOnlyCollection<ContentBlock> collection)
            => Math.Max(0, collection.Sum(x => Math.Max(1, (int)Math.Ceiling(x.Text.Length / charsPerToken))));
    }

    internal static string CompactWhitespace(string input)
    {
        var lines = input
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(line => string.Join(' ', line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return string.Join('\n', lines);
    }

    internal static string RebuildContent(IEnumerable<ContentBlock> blocks)
    {
        return string.Join("\n\n", blocks.Select(x => x.Text.Trim())).Trim();
    }

    internal static string EnforceTokenBudget(IReadOnlyCollection<ContentBlock> blocks, int targetTokens, double charsPerToken)
    {
        if (targetTokens <= 0 || blocks.Count == 0)
        {
            return string.Empty;
        }

        var maxChars = Math.Max(8, (int)Math.Floor(targetTokens * charsPerToken));
        if (targetTokens > 0)
        {
            maxChars = Math.Max(8, maxChars - 1);
        }

        var ordered = blocks
            .OrderByDescending(x => x.Index)
            .Select(x => x.Text.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var selected = new List<string>();
        var usedChars = 0;

        foreach (var text in ordered)
        {
            if (usedChars >= maxChars)
            {
                break;
            }

            var remaining = maxChars - usedChars;
            if (text.Length <= remaining)
            {
                selected.Add(text);
                usedChars += text.Length + 2;
                continue;
            }

            if (remaining >= 8)
            {
                selected.Add($"{text[..remaining].TrimEnd()}...");
                usedChars = maxChars;
            }

            break;
        }

        selected.Reverse();
        return string.Join("\n\n", selected).Trim();
    }

    internal static string FitToTokenBudget(string content, int targetTokens, double charsPerToken)
    {
        if (string.IsNullOrWhiteSpace(content) || targetTokens <= 0)
        {
            return content;
        }

        var maxChars = Math.Max(1, (int)Math.Floor(targetTokens * charsPerToken));
        var candidate = content.Length > maxChars
            ? content[..maxChars].TrimEnd()
            : content;

        while (candidate.Length > 0 && Math.Ceiling(candidate.Length / charsPerToken) > targetTokens)
        {
            candidate = candidate[..^1];
        }

        return string.IsNullOrWhiteSpace(candidate) ? content : candidate;
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
}

internal sealed class ContentBlock
{
    internal ContentBlock(int index, string text)
    {
        Index = index;
        Text = text;
    }

    internal int Index { get; }

    internal string Text { get; set; }

    internal bool IsProtected { get; set; }

    internal double Priority { get; set; }
}
