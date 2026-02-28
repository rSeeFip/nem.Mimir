namespace Mimir.Tui.Commands;

/// <summary>
/// The type of command parsed from user input.
/// </summary>
internal enum CommandType
{
    /// <summary>Regular chat message (not a slash command).</summary>
    Chat,

    /// <summary>/model [name] — switch LLM model.</summary>
    Model,

    /// <summary>/new [title] — create a new conversation.</summary>
    New,

    /// <summary>/list — list conversations.</summary>
    List,

    /// <summary>/archive — archive current conversation.</summary>
    Archive,

    /// <summary>/help — show available commands.</summary>
    Help,

    /// <summary>/quit or /exit — exit the application.</summary>
    Quit,
}

/// <summary>
/// Represents a parsed user command.
/// </summary>
internal sealed record ParsedCommand(CommandType Type, string Argument);

/// <summary>
/// Parses user input into structured commands.
/// </summary>
internal static class CommandParser
{
    /// <summary>
    /// Parses the given input string into a <see cref="ParsedCommand"/>.
    /// </summary>
    /// <param name="input">The raw user input.</param>
    /// <returns>A parsed command with its type and argument.</returns>
    public static ParsedCommand Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ParsedCommand(CommandType.Chat, string.Empty);
        }

        var trimmed = input.Trim();

        if (!trimmed.StartsWith('/'))
        {
            return new ParsedCommand(CommandType.Chat, trimmed);
        }

        var spaceIndex = trimmed.IndexOf(' ');
        var command = spaceIndex >= 0 ? trimmed[..spaceIndex].ToLowerInvariant() : trimmed.ToLowerInvariant();
        var argument = spaceIndex >= 0 ? trimmed[(spaceIndex + 1)..].Trim() : string.Empty;

        return command switch
        {
            "/model" => new ParsedCommand(CommandType.Model, argument),
            "/new" => new ParsedCommand(CommandType.New, argument),
            "/list" => new ParsedCommand(CommandType.List, argument),
            "/archive" => new ParsedCommand(CommandType.Archive, argument),
            "/help" => new ParsedCommand(CommandType.Help, argument),
            "/quit" => new ParsedCommand(CommandType.Quit, argument),
            "/exit" => new ParsedCommand(CommandType.Quit, argument),
            _ => new ParsedCommand(CommandType.Chat, trimmed),
        };
    }
}
