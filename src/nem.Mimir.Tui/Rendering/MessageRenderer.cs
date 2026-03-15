using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace nem.Mimir.Tui.Rendering;

/// <summary>
/// Renders chat messages using Spectre.Console markup with markdown-aware formatting.
/// </summary>
internal static partial class MessageRenderer
{
    /// <summary>
    /// Renders an assistant message with markdown-aware formatting.
    /// </summary>
    /// <param name="content">The raw message content from the LLM.</param>
    public static void RenderAssistantMessage(string content)
    {
        var formatted = FormatMarkdown(content);
        AnsiConsole.MarkupLine($"[green]Assistant:[/] {formatted}");
    }

    /// <summary>
    /// Renders a user message.
    /// </summary>
    /// <param name="content">The user's message content.</param>
    public static void RenderUserMessage(string content)
    {
        var escaped = Markup.Escape(content);
        AnsiConsole.MarkupLine($"[blue]You:[/] {escaped}");
    }

    /// <summary>
    /// Renders a system/info message.
    /// </summary>
    /// <param name="content">The system message content.</param>
    public static void RenderSystemMessage(string content)
    {
        var escaped = Markup.Escape(content);
        AnsiConsole.MarkupLine($"[grey]{escaped}[/]");
    }

    /// <summary>
    /// Renders an error message.
    /// </summary>
    /// <param name="content">The error message content.</param>
    public static void RenderError(string content)
    {
        var escaped = Markup.Escape(content);
        AnsiConsole.MarkupLine($"[red]Error:[/] {escaped}");
    }

    /// <summary>
    /// Renders a queue position indicator.
    /// </summary>
    /// <param name="position">The queue position number.</param>
    public static void RenderQueuePosition(int position)
    {
        AnsiConsole.MarkupLine($"[yellow]Queue position: {position}[/]");
    }

    /// <summary>
    /// Converts basic markdown syntax to Spectre.Console markup.
    /// Handles: code blocks, inline code, bold, and italic.
    /// </summary>
    /// <param name="text">Raw text with potential markdown.</param>
    /// <returns>Spectre.Console markup-formatted string.</returns>
    internal static string FormatMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = new StringBuilder();
        var lines = text.Split('\n');
        var inCodeBlock = false;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                if (inCodeBlock)
                {
                    result.AppendLine("[dim]───────────────────[/]");
                }
                else
                {
                    result.AppendLine("[dim]───────────────────[/]");
                }

                continue;
            }

            if (inCodeBlock)
            {
                var escaped = Markup.Escape(line);
                result.AppendLine($"[grey]{escaped}[/]");
                continue;
            }

            var processed = ProcessInlineFormatting(line);
            result.AppendLine(processed);
        }

        // Trim trailing newline
        if (result.Length > 0 && result[^1] == '\n')
        {
            result.Length--;
        }

        // Also trim trailing \r if present (Windows line endings)
        if (result.Length > 0 && result[^1] == '\r')
        {
            result.Length--;
        }

        return result.ToString();
    }

    /// <summary>
    /// Processes inline markdown formatting: **bold**, *italic*, `code`.
    /// </summary>
    private static string ProcessInlineFormatting(string line)
    {
        // First, escape all Spectre markup to prevent injection
        var escaped = Markup.Escape(line);

        // Process inline code: `code` → [grey on black]code[/]
        escaped = InlineCodeRegex().Replace(escaped, "[grey on black]$1[/]");

        // Process bold: **text** → [bold]text[/]
        escaped = BoldRegex().Replace(escaped, "[bold]$1[/]");

        // Process italic: *text* → [italic]text[/]
        escaped = ItalicRegex().Replace(escaped, "[italic]$1[/]");

        return escaped;
    }

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(?<!\*)\*([^*]+)\*(?!\*)")]
    private static partial Regex ItalicRegex();
}
