namespace nem.Mimir.Telegram.Canvas;

using System.Text;
using nem.Contracts.Canvas;

/// <summary>
/// Renders <see cref="ContentBlock"/> arrays to Telegram MarkdownV2 format.
/// Implements <see cref="ICanvasRenderer"/> with message splitting support for Telegram's 4096-char limit.
/// </summary>
internal sealed class TelegramCanvasRenderer : ICanvasRenderer
{
    /// <summary>
    /// Telegram's maximum message length in characters.
    /// </summary>
    internal const int MaxMessageLength = 4096;

    /// <summary>
    /// Divider string rendered for <see cref="DividerBlock"/>.
    /// </summary>
    internal const string DividerText = "———————————";

    /// <inheritdoc />
    public RenderFormat Format => RenderFormat.Markdown;

    /// <inheritdoc />
    public RenderedOutput Render(IReadOnlyList<ContentBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var rendered = RenderBlocksToStrings(blocks);
        var content = string.Join("\n\n", rendered);

        return new RenderedOutput(RenderFormat.Markdown, content);
    }

    /// <summary>
    /// Renders blocks and splits into multiple messages respecting Telegram's 4096-char limit.
    /// Splits at block boundaries; if a single block exceeds the limit, splits at line boundaries within that block.
    /// </summary>
    /// <param name="blocks">The content blocks to render.</param>
    /// <returns>List of message strings, each within the 4096-char limit.</returns>
    public IReadOnlyList<string> RenderMessages(IReadOnlyList<ContentBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        if (blocks.Count == 0)
            return [string.Empty];

        var rendered = RenderBlocksToStrings(blocks);
        return SplitMessages(rendered);
    }

    /// <summary>
    /// Renders each block to its MarkdownV2 string representation.
    /// </summary>
    private static List<string> RenderBlocksToStrings(IReadOnlyList<ContentBlock> blocks)
    {
        var results = new List<string>(blocks.Count);

        foreach (var block in blocks)
        {
            var rendered = RenderBlock(block);
            if (!string.IsNullOrEmpty(rendered))
            {
                results.Add(rendered);
            }
        }

        return results;
    }

    /// <summary>
    /// Renders a single content block to MarkdownV2 string.
    /// </summary>
    private static string RenderBlock(ContentBlock block)
    {
        return block switch
        {
            TextBlock tb => RenderTextBlock(tb),
            CodeBlock cb => RenderCodeBlock(cb),
            ImageBlock ib => RenderImageBlock(ib),
            TableBlock tab => RenderTableBlock(tab),
            ChartBlock chb => RenderChartBlock(chb),
            FormBlock fb => RenderFormBlock(fb),
            ProgressBlock pb => RenderProgressBlock(pb),
            LinkBlock lb => RenderLinkBlock(lb),
            DividerBlock => TelegramMarkdownEscaper.Escape(DividerText),
            _ => string.Empty,
        };
    }

    private static string RenderTextBlock(TextBlock block)
    {
        return TelegramMarkdownEscaper.Escape(block.Text);
    }

    private static string RenderCodeBlock(CodeBlock block)
    {
        var sb = new StringBuilder();
        sb.Append("```");
        sb.AppendLine(TelegramMarkdownEscaper.EscapeCodeBlock(block.Language));
        sb.AppendLine(TelegramMarkdownEscaper.EscapeCodeBlock(block.Code));
        sb.Append("```");
        return sb.ToString();
    }

    private static string RenderImageBlock(ImageBlock block)
    {
        var alt = string.IsNullOrWhiteSpace(block.AltText) ? "Image" : block.AltText;
        var escapedAlt = TelegramMarkdownEscaper.Escape($"\U0001f5bc {alt}");
        var escapedUrl = EscapeUrl(block.Url);
        return $"[{escapedAlt}]({escapedUrl})";
    }

    private static string RenderTableBlock(TableBlock block)
    {
        // Calculate column widths
        var columnCount = block.Headers.Length;
        var widths = new int[columnCount];

        for (int i = 0; i < columnCount; i++)
        {
            widths[i] = block.Headers[i].Length;
        }

        foreach (var row in block.Rows)
        {
            for (int i = 0; i < columnCount && i < row.Length; i++)
            {
                var cellLen = (row[i] ?? string.Empty).Length;
                if (cellLen > widths[i])
                    widths[i] = cellLen;
            }
        }

        var sb = new StringBuilder();

        // Render as monospace (code block) for alignment
        sb.Append("```");
        sb.AppendLine();

        // Header row
        sb.Append('|');
        for (int i = 0; i < columnCount; i++)
        {
            sb.Append(' ');
            sb.Append(block.Headers[i].PadRight(widths[i]));
            sb.Append(" |");
        }

        sb.AppendLine();

        // Separator row
        sb.Append('|');
        for (int i = 0; i < columnCount; i++)
        {
            sb.Append('-', widths[i] + 2);
            sb.Append('|');
        }

        sb.AppendLine();

        // Data rows
        foreach (var row in block.Rows)
        {
            sb.Append('|');
            for (int i = 0; i < columnCount; i++)
            {
                var cell = i < row.Length ? (row[i] ?? string.Empty) : string.Empty;
                sb.Append(' ');
                sb.Append(cell.PadRight(widths[i]));
                sb.Append(" |");
            }

            sb.AppendLine();
        }

        // Caption
        if (!string.IsNullOrWhiteSpace(block.Caption))
        {
            sb.AppendLine();
            sb.AppendLine(TelegramMarkdownEscaper.EscapeCodeBlock(block.Caption));
        }

        sb.Append("```");

        return sb.ToString();
    }

    private static string RenderChartBlock(ChartBlock block)
    {
        var sb = new StringBuilder();
        var title = string.IsNullOrWhiteSpace(block.Title) ? "Chart" : block.Title;
        sb.Append(TelegramMarkdownEscaper.Escape($"\U0001f4ca {title}"));
        sb.AppendLine();
        sb.Append(TelegramMarkdownEscaper.Escape($"Type: {block.ChartType}"));
        sb.AppendLine();
        sb.Append(TelegramMarkdownEscaper.Escape($"Data: {block.DataJson}"));
        return sb.ToString();
    }

    private static string RenderFormBlock(FormBlock block)
    {
        var sb = new StringBuilder();
        sb.Append(TelegramMarkdownEscaper.Escape($"\U0001f4cb {block.FormId}"));

        foreach (var field in block.Fields)
        {
            sb.AppendLine();
            var required = field.Required ? " *" : "";
            sb.Append(TelegramMarkdownEscaper.Escape($"• {field.Label} ({field.FieldType}){required}"));
        }

        return sb.ToString();
    }

    private static string RenderProgressBlock(ProgressBlock block)
    {
        var pct = Math.Clamp(block.Percent, 0, 100);
        var filledCount = (int)Math.Round(pct / 10.0);
        var emptyCount = 10 - filledCount;

        var bar = new string('\u2588', filledCount) + new string('\u2591', emptyCount);
        var status = string.IsNullOrWhiteSpace(block.Status)
            ? string.Empty
            : $" — {block.Status}";

        return TelegramMarkdownEscaper.Escape($"{block.Label}: [{bar}] {pct:0}%{status}");
    }

    private static string RenderLinkBlock(LinkBlock block)
    {
        var escapedText = TelegramMarkdownEscaper.Escape(block.Text);
        var escapedUrl = EscapeUrl(block.Url);
        return $"[{escapedText}]({escapedUrl})";
    }

    /// <summary>
    /// Escapes URL for Telegram MarkdownV2 link context.
    /// Only ) and \ need escaping inside the URL part of [text](url).
    /// </summary>
    private static string EscapeUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        return url.Replace("\\", "\\\\").Replace(")", "\\)");
    }

    /// <summary>
    /// Splits rendered block strings into messages respecting the 4096-char limit.
    /// </summary>
    private static IReadOnlyList<string> SplitMessages(List<string> renderedBlocks)
    {
        if (renderedBlocks.Count == 0)
            return [string.Empty];

        var messages = new List<string>();
        var currentMessage = new StringBuilder();

        foreach (var blockText in renderedBlocks)
        {
            // If single block exceeds limit, split it at line boundaries
            if (blockText.Length > MaxMessageLength)
            {
                // Flush current message first
                if (currentMessage.Length > 0)
                {
                    messages.Add(currentMessage.ToString());
                    currentMessage.Clear();
                }

                SplitLargeBlock(blockText, messages);
                continue;
            }

            // Check if adding this block would exceed the limit
            var separator = currentMessage.Length > 0 ? "\n\n" : "";
            var newLength = currentMessage.Length + separator.Length + blockText.Length;

            if (newLength > MaxMessageLength)
            {
                // Flush current message and start new one
                if (currentMessage.Length > 0)
                {
                    messages.Add(currentMessage.ToString());
                    currentMessage.Clear();
                }
            }

            if (currentMessage.Length > 0)
            {
                currentMessage.Append("\n\n");
            }

            currentMessage.Append(blockText);
        }

        // Flush remaining
        if (currentMessage.Length > 0)
        {
            messages.Add(currentMessage.ToString());
        }

        return messages.Count == 0 ? [string.Empty] : messages;
    }

    /// <summary>
    /// Splits a single large block at line boundaries to fit within the message limit.
    /// </summary>
    private static void SplitLargeBlock(string blockText, List<string> messages)
    {
        var lines = blockText.Split('\n');
        var current = new StringBuilder();

        foreach (var line in lines)
        {
            var separator = current.Length > 0 ? "\n" : "";
            var newLength = current.Length + separator.Length + line.Length;

            if (newLength > MaxMessageLength && current.Length > 0)
            {
                messages.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append('\n');
            }

            current.Append(line);
        }

        if (current.Length > 0)
        {
            messages.Add(current.ToString());
        }
    }
}
