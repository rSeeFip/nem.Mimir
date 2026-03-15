using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using nem.Mimir.Telegram.Services;

namespace nem.Mimir.Telegram.Handlers;

/// <summary>
/// Handles Telegram bot commands: /start, /new, /list, /model, /help.
/// </summary>
internal sealed class CommandHandler : ICommandHandler
{
    private readonly MimirApiClient _apiClient;
    private readonly UserStateManager _stateManager;
    private readonly AuthenticationService _authService;
    private readonly ILogger<CommandHandler> _logger;

    public CommandHandler(
        MimirApiClient apiClient,
        UserStateManager stateManager,
        AuthenticationService authService,
        ILogger<CommandHandler> logger)
    {
        _apiClient = apiClient;
        _stateManager = stateManager;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Determines if the message text is a bot command.
    /// </summary>
    public static bool IsCommand(string? text)
    {
        return text is not null && text.StartsWith('/');
    }

    /// <summary>
    /// Instance implementation of IsCommand from ICommandHandler interface.
    /// </summary>
    bool ICommandHandler.IsCommand(string? text)
    {
        return IsCommand(text);
    }

    /// <summary>
    /// Dispatches the command to the appropriate handler.
    /// </summary>
    public async Task HandleAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        var text = message.Text ?? string.Empty;
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant().Split('@')[0]; // Handle @botname suffix
        var argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? chatId;

        _logger.LogDebug("Handling command {Command} from user {UserId}", command, userId);

        switch (command)
        {
            case "/start":
                await HandleStartAsync(bot, chatId, userId, argument, ct);
                break;
            case "/new":
                await HandleNewAsync(bot, chatId, userId, argument, ct);
                break;
            case "/list":
                await HandleListAsync(bot, chatId, userId, ct);
                break;
            case "/model":
                await HandleModelAsync(bot, chatId, userId, argument, ct);
                break;
            case "/help":
                await HandleHelpAsync(bot, chatId, ct);
                break;
            default:
                await bot.SendMessage(chatId, "Unknown command. Use /help for available commands.", cancellationToken: ct);
                break;
        }
    }

    private async Task HandleStartAsync(ITelegramBotClient bot, long chatId, long userId, string argument, CancellationToken ct)
    {
        if (_stateManager.IsAuthenticated(userId))
        {
            await bot.SendMessage(chatId,
                "✅ You are already authenticated.\n\nUse /new to start a conversation or /help for all commands.",
                cancellationToken: ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(argument))
        {
            // User is providing credentials in format: username:password
            var credParts = argument.Split(':', 2);
            if (credParts.Length == 2)
            {
                await bot.SendMessage(chatId, "🔐 Authenticating...", cancellationToken: ct);

                var token = await _authService.AuthenticateWithKeycloakAsync(credParts[0], credParts[1], ct);
                if (token is not null)
                {
                    _stateManager.SetAuthenticated(userId, token);
                    await bot.SendMessage(chatId,
                        "✅ Authentication successful!\n\nUse /new to start a new conversation.",
                        cancellationToken: ct);
                    return;
                }

                await bot.SendMessage(chatId,
                    "❌ Authentication failed. Please check your credentials and try again.",
                    cancellationToken: ct);
                return;
            }
        }

        var authCode = _authService.GenerateAuthCode(userId);
        await bot.SendMessage(chatId,
            $"👋 Welcome to Mimir!\n\n" +
            $"To get started, authenticate with your Mimir credentials:\n\n" +
            $"Send: /start username:password\n\n" +
            $"Your one-time auth code is: <code>{authCode}</code>\n" +
            $"(Expires in 5 minutes)\n\n" +
            $"After authenticating, use /help to see available commands.",
            parseMode: global::Telegram.Bot.Types.Enums.ParseMode.Html,
            cancellationToken: ct);
    }

    private async Task HandleNewAsync(ITelegramBotClient bot, long chatId, long userId, string argument, CancellationToken ct)
    {
        if (!EnsureAuthenticated(bot, chatId, userId, ct, out var state))
            return;

        var title = string.IsNullOrWhiteSpace(argument) ? $"Telegram Chat {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}" : argument;

        _apiClient.SetBearerToken(state!.BearerToken!);
        var conversation = await _apiClient.CreateConversationAsync(title, state.SelectedModel, ct);

        if (conversation is null)
        {
            await bot.SendMessage(chatId, "❌ Failed to create conversation. Please try again.", cancellationToken: ct);
            return;
        }

        _stateManager.SetCurrentConversation(userId, conversation.Id, conversation.Title);

        await bot.SendMessage(chatId,
            $"📝 New conversation created: <b>{EscapeHtml(conversation.Title)}</b>\n\n" +
            $"You can now send messages and I'll forward them to the AI.",
            parseMode: global::Telegram.Bot.Types.Enums.ParseMode.Html,
            cancellationToken: ct);
    }

    private async Task HandleListAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        if (!EnsureAuthenticated(bot, chatId, userId, ct, out var state))
            return;

        _apiClient.SetBearerToken(state!.BearerToken!);
        var conversations = await _apiClient.ListConversationsAsync(1, 10, ct);

        if (conversations is null || conversations.Items.Count == 0)
        {
            await bot.SendMessage(chatId, "📭 No conversations found. Use /new to start one!", cancellationToken: ct);
            return;
        }

        var lines = new List<string> { "📋 <b>Your Conversations:</b>\n" };
        var currentState = _stateManager.GetState(userId);

        foreach (var conv in conversations.Items)
        {
            var isCurrent = currentState?.CurrentConversationId == conv.Id;
            var marker = isCurrent ? " 👈" : "";
            lines.Add($"• <b>{EscapeHtml(conv.Title)}</b> ({conv.MessageCount} msgs){marker}");
        }

        if (conversations.TotalCount > 10)
        {
            lines.Add($"\n<i>Showing 10 of {conversations.TotalCount} conversations</i>");
        }

        await bot.SendMessage(chatId, string.Join('\n', lines),
            parseMode: global::Telegram.Bot.Types.Enums.ParseMode.Html,
            cancellationToken: ct);
    }

    private async Task HandleModelAsync(ITelegramBotClient bot, long chatId, long userId, string argument, CancellationToken ct)
    {
        if (!EnsureAuthenticated(bot, chatId, userId, ct, out var state))
            return;

        _apiClient.SetBearerToken(state!.BearerToken!);

        if (string.IsNullOrWhiteSpace(argument))
        {
            // List available models
            var models = await _apiClient.GetModelsAsync(ct);
            if (models is null || models.Count == 0)
            {
                await bot.SendMessage(chatId, "❌ No models available.", cancellationToken: ct);
                return;
            }

            var currentModel = _stateManager.GetState(userId)?.SelectedModel;
            var lines = new List<string> { "🤖 <b>Available Models:</b>\n" };

            foreach (var model in models)
            {
                var isCurrent = string.Equals(model.Id, currentModel, StringComparison.OrdinalIgnoreCase);
                var marker = isCurrent ? " ✅" : "";
                var available = model.IsAvailable ? "" : " (unavailable)";
                lines.Add($"• <code>{EscapeHtml(model.Id)}</code> — {EscapeHtml(model.Name)}{available}{marker}");
            }

            lines.Add("\nTo select a model: /model <code>model-id</code>");

            await bot.SendMessage(chatId, string.Join('\n', lines),
                parseMode: global::Telegram.Bot.Types.Enums.ParseMode.Html,
                cancellationToken: ct);
            return;
        }

        // Set model
        _stateManager.SetSelectedModel(userId, argument);
        await bot.SendMessage(chatId,
            $"✅ Model set to <code>{EscapeHtml(argument)}</code>",
            parseMode: global::Telegram.Bot.Types.Enums.ParseMode.Html,
            cancellationToken: ct);
    }

    private static async Task HandleHelpAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        const string helpText =
            "🤖 <b>Mimir Bot Commands:</b>\n\n" +
            "/start — Authenticate with Mimir\n" +
            "/new [title] — Create a new conversation\n" +
            "/list — List your conversations\n" +
            "/model [name] — View or select an AI model\n" +
            "/help — Show this help message\n\n" +
            "Just send any text message to chat with the AI in your current conversation.";

        await bot.SendMessage(chatId, helpText,
            parseMode: global::Telegram.Bot.Types.Enums.ParseMode.Html,
            cancellationToken: ct);
    }

    private bool EnsureAuthenticated(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct, out UserState? state)
    {
        state = _stateManager.GetState(userId);
        if (state is { IsAuthenticated: true, BearerToken: not null })
        {
            return true;
        }

        // Fire-and-forget the message send (we're in a sync method that returns bool)
        _ = bot.SendMessage(chatId, "🔒 Please authenticate first using /start", cancellationToken: ct);
        return false;
    }

    internal static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}
