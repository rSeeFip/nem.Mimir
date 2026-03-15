using Telegram.Bot;
using Telegram.Bot.Types;

namespace nem.Mimir.Telegram.Handlers;

/// <summary>
/// Interface for handling regular (non-command) text messages.
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// Processes a text message from a Telegram user.
    /// </summary>
    Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken);
}
