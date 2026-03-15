namespace nem.Mimir.Telegram.Canvas;

/// <summary>
/// Utility for escaping text for Telegram MarkdownV2 format.
/// Telegram MarkdownV2 requires escaping these characters: _ * [ ] ( ) ~ ` > # + - = | { } . !
/// Inside code blocks (```...``` or `...`), only ` and \ need escaping.
/// </summary>
internal static class TelegramMarkdownEscaper
{
    /// <summary>
    /// Characters that must be escaped in normal MarkdownV2 text.
    /// </summary>
    private static readonly char[] SpecialChars =
        ['_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!', '\\'];

    /// <summary>
    /// Escapes all MarkdownV2 special characters in the given text.
    /// </summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>Escaped text safe for Telegram MarkdownV2.</returns>
    public static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Pre-calculate capacity: worst case every char needs escaping
        var sb = new System.Text.StringBuilder(text.Length * 2);

        foreach (var ch in text)
        {
            if (Array.IndexOf(SpecialChars, ch) >= 0)
            {
                sb.Append('\\');
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escapes text for use inside a code block (``` or `).
    /// Only backtick and backslash need escaping inside code blocks.
    /// </summary>
    /// <param name="text">The text to escape for code block context.</param>
    /// <returns>Escaped text safe for inside a MarkdownV2 code block.</returns>
    public static string EscapeCodeBlock(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new System.Text.StringBuilder(text.Length * 2);

        foreach (var ch in text)
        {
            if (ch is '`' or '\\')
            {
                sb.Append('\\');
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }
}
