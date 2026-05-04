using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Common.Sanitization;

namespace nem.Mimir.Infrastructure.Sanitization;

public sealed partial class StreamingSanitizer
{
    internal const int DefaultRollingWindowSize = 256;

    private readonly SanitizationOptions _options;
    private readonly ILogger<StreamingSanitizer> _logger;
    private readonly int _rollingWindowSize;
    private readonly Dictionary<string, string> _rollingWindows = new(StringComparer.OrdinalIgnoreCase);

    public StreamingSanitizer(
        IOptions<SanitizationOptions> options,
        ILogger<StreamingSanitizer> logger,
        int rollingWindowSize = DefaultRollingWindowSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rollingWindowSize);

        _options = options.Value;
        _logger = logger;
        _rollingWindowSize = rollingWindowSize;
    }

    public SanitizedChunkResult SanitizeChunk(string chunk, string channel)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        if (chunk.Length == 0)
        {
            return new SanitizedChunkResult(chunk, WasModified: false, DetectedPatterns: [], ShouldTerminate: false);
        }

        var mode = _options.EnforcementEnabled
            ? _options.GetModeForChannel(channel)
            : SanitizationMode.Log;

        var existingWindow = GetRollingWindow(channel);
        var candidate = string.Concat(existingWindow, chunk);
        var detections = FindDetections(candidate, existingWindow.Length, chunk.Length);

        if (detections.Count == 0)
        {
            UpdateRollingWindow(channel, string.Concat(existingWindow, chunk));
            return new SanitizedChunkResult(chunk, WasModified: false, DetectedPatterns: [], ShouldTerminate: false);
        }

        var detectedPatterns = detections
            .Select(static detection => detection.MatchedText)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (mode == SanitizationMode.Block)
        {
            UpdateRollingWindow(channel, existingWindow);
            return new SanitizedChunkResult(string.Empty, WasModified: true, DetectedPatterns: detectedPatterns, ShouldTerminate: true);
        }

        if (mode == SanitizationMode.Log)
        {
            _logger.LogWarning(
                "Streaming injection pattern detected on channel {Channel}: {Patterns}",
                channel,
                detectedPatterns);

            UpdateRollingWindow(channel, string.Concat(existingWindow, chunk));
            return new SanitizedChunkResult(chunk, WasModified: false, DetectedPatterns: detectedPatterns, ShouldTerminate: false);
        }

        var cleanChunk = StripDetectedSegments(chunk, detections);
        UpdateRollingWindow(channel, string.Concat(existingWindow, cleanChunk));

        return new SanitizedChunkResult(
            cleanChunk,
            WasModified: !string.Equals(cleanChunk, chunk, StringComparison.Ordinal),
            DetectedPatterns: detectedPatterns,
            ShouldTerminate: false);
    }

    private string GetRollingWindow(string channel)
        => _rollingWindows.TryGetValue(channel, out var rollingWindow)
            ? rollingWindow
            : string.Empty;

    private void UpdateRollingWindow(string channel, string content)
    {
        _rollingWindows[channel] = content.Length <= _rollingWindowSize
            ? content
            : content[^_rollingWindowSize..];
    }

    private static List<ChunkDetection> FindDetections(string candidate, int windowLength, int chunkLength)
    {
        var detections = new List<ChunkDetection>();

        foreach (var regex in EnumerateDetectionPatterns())
        {
            foreach (Match match in regex.Matches(candidate))
            {
                if (!match.Success)
                {
                    continue;
                }

                var chunkStart = Math.Max(0, match.Index - windowLength);
                var chunkEnd = Math.Min(chunkLength, match.Index + match.Length - windowLength);

                if (chunkEnd <= chunkStart)
                {
                    continue;
                }

                detections.Add(new ChunkDetection(chunkStart, chunkEnd, match.Value));
            }
        }

        return detections;
    }

    private static string StripDetectedSegments(string chunk, List<ChunkDetection> detections)
    {
        var mergedRanges = detections
            .OrderBy(static detection => detection.Start)
            .Select(static detection => (Start: detection.Start, End: detection.End))
            .Aggregate(
                new List<(int Start, int End)>(),
                static (ranges, current) =>
                {
                    if (ranges.Count == 0)
                    {
                        ranges.Add(current);
                        return ranges;
                    }

                    var last = ranges[^1];
                    if (current.Start <= last.End)
                    {
                        ranges[^1] = (last.Start, Math.Max(last.End, current.End));
                    }
                    else
                    {
                        ranges.Add(current);
                    }

                    return ranges;
                });

        if (mergedRanges.Count == 0)
        {
            return chunk;
        }

        var builder = new StringBuilder(chunk.Length);
        var position = 0;

        foreach (var (start, end) in mergedRanges)
        {
            if (start > position)
            {
                builder.Append(chunk, position, start - position);
            }

            position = Math.Max(position, end);
        }

        if (position < chunk.Length)
        {
            builder.Append(chunk, position, chunk.Length - position);
        }

        return builder.ToString();
    }

    private static IEnumerable<Regex> EnumerateDetectionPatterns()
    {
        yield return PromptInjectionPatternRegex();
        yield return PromptInjectionMarkerRegex();
    }

    [GeneratedRegex(
        @"ignore\s+previous\s+instructions|ignore\s+all\s+instructions|system\s+prompt|you\s+are\s+now|act\s+as\s+if|forget\s+(everything|all|your|previous)|disregard\s+(all|previous|your)|override\s+instructions|\[SYSTEM\]|\[INST\]|<<SYS>>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PromptInjectionPatternRegex();

    [GeneratedRegex(
        @"\[SYSTEM\]|\[INST\]|\[/INST\]|<<SYS>>|<</SYS>>|</s>|\[INST\s*\]",
        RegexOptions.Compiled)]
    private static partial Regex PromptInjectionMarkerRegex();

    private sealed record ChunkDetection(int Start, int End, string MatchedText);
}

public sealed record SanitizedChunkResult(
    string CleanContent,
    bool WasModified,
    IReadOnlyList<string> DetectedPatterns,
    bool ShouldTerminate);
