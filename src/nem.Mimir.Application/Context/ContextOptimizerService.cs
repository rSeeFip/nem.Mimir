using System.Text;
using Microsoft.Extensions.Logging;
using nem.Contracts.TokenOptimization;

namespace nem.Mimir.Application.Context;

/// <summary>
/// Optimizes context content through distillation, pruning, and compression to reduce token usage.
/// </summary>
public sealed class ContextOptimizerService : IContextOptimizer
{
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

            var blocks = ContextOptimizerHelpers.ParseBlocks(content);
            if (blocks.Count == 0)
            {
                return Task.FromResult(content);
            }

            ContextOptimizerHelpers.MarkProtectedBlocks(blocks);
            ContextOptimizerHelpers.ScoreBlocks(blocks);

            var charsPerToken = _options.CharsPerToken <= 0 ? 4.0d : _options.CharsPerToken;
            var kept = ContextOptimizerHelpers.BuildDistilledSet(blocks, targetTokens, charsPerToken);
            var distilled = ContextOptimizerHelpers.RebuildContent(kept.OrderBy(x => x.Index));
            if (EstimateTokens(distilled) > targetTokens)
            {
                distilled = ContextOptimizerHelpers.EnforceTokenBudget(kept, targetTokens, charsPerToken);
            }

            distilled = ContextOptimizerHelpers.FitToTokenBudget(distilled, targetTokens, charsPerToken);

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

            var blocks = ContextOptimizerHelpers.ParseBlocks(content);
            if (blocks.Count == 0)
            {
                return Task.FromResult(content);
            }

            ContextOptimizerHelpers.MarkProtectedBlocks(blocks);

            foreach (var block in blocks)
            {
                block.Text = ContextOptimizerHelpers.CompactWhitespace(block.Text);
            }

            var compressed = ContextOptimizerHelpers.RebuildContent(blocks.OrderBy(x => x.Index));
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

        var blocks = ContextOptimizerHelpers.ParseBlocks(content);
        if (blocks.Count == 0)
        {
            return content;
        }

        ContextOptimizerHelpers.MarkProtectedBlocks(blocks);
        var kept = blocks.ToList();

        foreach (var block in blocks.OrderBy(x => x.Index))
        {
            if (EstimateTokens(ContextOptimizerHelpers.RebuildContent(kept.OrderBy(x => x.Index))) <= targetTokens)
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
        var result = ContextOptimizerHelpers.RebuildContent(kept.OrderBy(x => x.Index));
        return string.IsNullOrWhiteSpace(result) ? content : result;
    }

    private async Task<string> RemoveLeastRelevantAsync(string content, int targetTokens, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var blocks = ContextOptimizerHelpers.ParseBlocks(content);
        if (blocks.Count == 0)
        {
            return content;
        }

        ContextOptimizerHelpers.MarkProtectedBlocks(blocks);
        ContextOptimizerHelpers.ScoreBlocks(blocks);
        var kept = blocks.ToList();

        foreach (var block in blocks
                     .Where(x => !x.IsProtected)
                     .OrderBy(x => x.Priority)
                     .ThenBy(x => x.Index))
        {
            if (EstimateTokens(ContextOptimizerHelpers.RebuildContent(kept.OrderBy(x => x.Index))) <= targetTokens)
            {
                break;
            }

            kept.Remove(block);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        var result = ContextOptimizerHelpers.RebuildContent(kept.OrderBy(x => x.Index));
        return string.IsNullOrWhiteSpace(result) ? content : result;
    }

    private string TruncateContent(string content, int targetTokens)
    {
        var blocks = ContextOptimizerHelpers.ParseBlocks(content);
        if (blocks.Count == 0)
        {
            return content;
        }

        ContextOptimizerHelpers.MarkProtectedBlocks(blocks);
        var protectedBlocks = blocks.Where(x => x.IsProtected).OrderBy(x => x.Index).ToList();
        var protectedContent = ContextOptimizerHelpers.RebuildContent(protectedBlocks);

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

}
