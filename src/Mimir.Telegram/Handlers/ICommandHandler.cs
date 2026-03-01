using Telegram.Bot;
using Telegram.Bot.Types;

namespace Mimir.Telegram.Handlers;

/// <summary>
/// Interface for handling Telegram bot commands.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Determines if the message text is a bot command.
    /// </summary>
    bool IsCommand(string? text);

    /// <summary>
    /// Dispatches the command to the appropriate handler.
    /// </summary>
    Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken);
}
