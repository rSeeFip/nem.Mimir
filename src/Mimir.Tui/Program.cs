using System.Text;
using Microsoft.Extensions.Configuration;
using Mimir.Tui.Commands;
using Mimir.Tui.Models;
using Mimir.Tui.Rendering;
using Mimir.Tui.Services;
using Spectre.Console;

namespace Mimir.Tui;

/// <summary>
/// Entry point for the Mimir TUI (Terminal User Interface) client.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        var settings = new TuiSettings();
        configuration.GetSection(TuiSettings.SectionName).Bind(settings);

        using var httpClient = new HttpClient { BaseAddress = new Uri(settings.ApiBaseUrl) };
        using var authHttpClient = new HttpClient();

        var authService = new AuthenticationService(authHttpClient, settings);
        var conversationService = new ConversationApiService(httpClient, authService);
        var modelService = new ModelService(httpClient, authService, settings);
        var chatStreamService = new ChatStreamService(settings, authService);

        try
        {
            RenderWelcomeBanner();

            // Login flow
            if (!await PerformLoginAsync(authService))
            {
                MessageRenderer.RenderError("Authentication failed. Exiting.");
                return 1;
            }

            // Connect to SignalR hub
            await ConnectToHubAsync(chatStreamService);

            // Main interaction loop
            return await RunMainLoopAsync(
                authService, conversationService, modelService, chatStreamService);
        }
        catch (Exception ex)
        {
            MessageRenderer.RenderError($"Fatal error: {ex.Message}");
            return 1;
        }
        finally
        {
            await chatStreamService.DisposeAsync();
        }
    }

    private static void RenderWelcomeBanner()
    {
        var rule = new Rule("[green]Mimir TUI[/]").RuleStyle("grey");
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine("[grey]Type [bold]/help[/] for available commands.[/]");
        AnsiConsole.WriteLine();
    }

    private static async Task<bool> PerformLoginAsync(AuthenticationService authService)
    {
        AnsiConsole.MarkupLine("[bold]Login to Mimir[/]");
        var username = AnsiConsole.Ask<string>("[blue]Username:[/]");
        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("[blue]Password:[/]").Secret());

        return await AnsiConsole.Status()
            .StartAsync("Authenticating...", async _ =>
            {
                return await authService.LoginAsync(username, password);
            });
    }

    private static async Task ConnectToHubAsync(ChatStreamService chatStreamService)
    {
        await AnsiConsole.Status()
            .StartAsync("Connecting to chat hub...", async _ =>
            {
                await chatStreamService.ConnectAsync();
            });

        MessageRenderer.RenderSystemMessage("Connected to chat hub.");
        AnsiConsole.WriteLine();
    }

    private static async Task<int> RunMainLoopAsync(
        AuthenticationService authService,
        ConversationApiService conversationService,
        ModelService modelService,
        ChatStreamService chatStreamService)
    {
        Guid? currentConversationId = null;
        string? currentConversationTitle = null;

        while (true)
        {
            var prompt = currentConversationTitle is not null
                ? $"[green]{Markup.Escape(currentConversationTitle)}[/]> "
                : "[green]mimir[/]> ";

            var input = AnsiConsole.Prompt(new TextPrompt<string>(prompt).AllowEmpty());
            var command = CommandParser.Parse(input);

            switch (command.Type)
            {
                case CommandType.Quit:
                    MessageRenderer.RenderSystemMessage("Goodbye!");
                    return 0;

                case CommandType.Help:
                    RenderHelp(modelService);
                    break;

                case CommandType.Model:
                    HandleModelCommand(command.Argument, modelService);
                    break;

                case CommandType.New:
                    (currentConversationId, currentConversationTitle) =
                        await HandleNewConversationAsync(command.Argument, conversationService, modelService);
                    break;

                case CommandType.List:
                    await HandleListConversationsAsync(conversationService);
                    break;

                case CommandType.Archive:
                    if (currentConversationId is not null)
                    {
                        await HandleArchiveAsync(currentConversationId.Value, conversationService);
                        currentConversationId = null;
                        currentConversationTitle = null;
                    }
                    else
                    {
                        MessageRenderer.RenderError("No active conversation to archive. Use /new to create one.");
                    }

                    break;

                case CommandType.Chat:
                    if (string.IsNullOrWhiteSpace(command.Argument))
                    {
                        break;
                    }

                    if (currentConversationId is null)
                    {
                        // Auto-create conversation
                        var title = command.Argument.Length > 50
                            ? command.Argument[..50] + "..."
                            : command.Argument;
                        (currentConversationId, currentConversationTitle) =
                            await HandleNewConversationAsync(title, conversationService, modelService);

                        if (currentConversationId is null)
                        {
                            break;
                        }
                    }

                    MessageRenderer.RenderUserMessage(command.Argument);
                    await StreamChatResponseAsync(
                        currentConversationId.Value.ToString(),
                        command.Argument,
                        modelService.CurrentModel,
                        chatStreamService);
                    break;
            }
        }
    }

    private static void RenderHelp(ModelService modelService)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Command[/]")
            .AddColumn("[bold]Description[/]");

        table.AddRow("/model [name]", "Switch model (phi-4-mini, qwen-2.5-72b, qwen-2.5-coder-32b)");
        table.AddRow("/new [title]", "Create a new conversation");
        table.AddRow("/list", "List your conversations");
        table.AddRow("/archive", "Archive current conversation");
        table.AddRow("/help", "Show this help");
        table.AddRow("/quit or /exit", "Exit Mimir TUI");

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]Current model: [bold]{Markup.Escape(modelService.CurrentModel)}[/][/]");
        AnsiConsole.WriteLine();
    }

    private static void HandleModelCommand(string argument, ModelService modelService)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            AnsiConsole.MarkupLine($"[grey]Current model: [bold]{Markup.Escape(modelService.CurrentModel)}[/][/]");
            AnsiConsole.MarkupLine("[grey]Available: phi-4-mini, qwen-2.5-72b, qwen-2.5-coder-32b[/]");
            return;
        }

        if (modelService.TrySetModel(argument))
        {
            AnsiConsole.MarkupLine($"[green]Model switched to [bold]{Markup.Escape(modelService.CurrentModel)}[/][/]");
        }
        else
        {
            MessageRenderer.RenderError(
                $"Unknown model '{argument}'. Available: phi-4-mini, qwen-2.5-72b, qwen-2.5-coder-32b");
        }
    }

    private static async Task<(Guid? Id, string? Title)> HandleNewConversationAsync(
        string title,
        ConversationApiService conversationService,
        ModelService modelService)
    {
        var effectiveTitle = string.IsNullOrWhiteSpace(title) ? "New Conversation" : title;

        try
        {
            var conversation = await conversationService.CreateConversationAsync(
                effectiveTitle, modelService.CurrentModel);

            if (conversation is not null)
            {
                AnsiConsole.MarkupLine($"[green]Created conversation: [bold]{Markup.Escape(conversation.Title)}[/][/]");
                return (conversation.Id, conversation.Title);
            }

            MessageRenderer.RenderError("Failed to create conversation.");
            return (null, null);
        }
        catch (HttpRequestException ex)
        {
            MessageRenderer.RenderError($"Failed to create conversation: {ex.Message}");
            return (null, null);
        }
    }

    private static async Task HandleListConversationsAsync(ConversationApiService conversationService)
    {
        try
        {
            var result = await conversationService.ListConversationsAsync();

            if (result is null || result.Items.Count == 0)
            {
                MessageRenderer.RenderSystemMessage("No conversations found.");
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Title[/]")
                .AddColumn("[bold]Messages[/]")
                .AddColumn("[bold]Status[/]")
                .AddColumn("[bold]Created[/]");

            foreach (var conv in result.Items)
            {
                table.AddRow(
                    Markup.Escape(conv.Title),
                    conv.MessageCount.ToString(),
                    conv.Status,
                    conv.CreatedAt.LocalDateTime.ToString("g"));
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(
                $"[grey]Page {result.PageNumber} of {result.TotalPages} ({result.TotalCount} total)[/]");
        }
        catch (HttpRequestException ex)
        {
            MessageRenderer.RenderError($"Failed to list conversations: {ex.Message}");
        }
    }

    private static async Task HandleArchiveAsync(
        Guid conversationId,
        ConversationApiService conversationService)
    {
        try
        {
            await conversationService.ArchiveConversationAsync(conversationId);
            AnsiConsole.MarkupLine("[green]Conversation archived.[/]");
        }
        catch (HttpRequestException ex)
        {
            MessageRenderer.RenderError($"Failed to archive conversation: {ex.Message}");
        }
    }

    private static async Task StreamChatResponseAsync(
        string conversationId,
        string content,
        string model,
        ChatStreamService chatStreamService)
    {
        var responseBuilder = new StringBuilder();

        try
        {
            AnsiConsole.Markup("[green]Assistant:[/] ");

            await foreach (var token in chatStreamService.StreamMessageAsync(
                               conversationId, content, model))
            {
                if (token.QueuePosition is > 1)
                {
                    MessageRenderer.RenderQueuePosition(token.QueuePosition.Value);
                    continue;
                }

                if (!string.IsNullOrEmpty(token.Token))
                {
                    responseBuilder.Append(token.Token);
                    // Write raw token for real-time streaming display
                    Console.Write(token.Token);
                }

                if (token.IsComplete)
                {
                    Console.WriteLine();
                    break;
                }
            }

            AnsiConsole.WriteLine();
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.WriteLine();
            MessageRenderer.RenderSystemMessage("Streaming cancelled.");
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            MessageRenderer.RenderError($"Streaming error: {ex.Message}");
        }
    }
}
