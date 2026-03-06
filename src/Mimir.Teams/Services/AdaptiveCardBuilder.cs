using System.Text.Json;
using nem.Contracts.Content;

namespace Mimir.Teams.Services;

internal sealed class AdaptiveCardBuilder
{
    public string Build(IContentPayload? content)
    {
        var body = content switch
        {
            TextContent text => BuildTextBody(text),
            CanvasContent canvas => BuildCanvasBody(canvas),
            _ => BuildFallbackBody(),
        };

        var card = new Dictionary<string, object>
        {
            ["type"] = "AdaptiveCard",
            ["version"] = "1.4",
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["body"] = body,
        };

        return JsonSerializer.Serialize(card, SerializerOptions);
    }

    private static object[] BuildTextBody(TextContent text) =>
    [
        new Dictionary<string, object>
        {
            ["type"] = "TextBlock",
            ["text"] = text.Text,
            ["wrap"] = true,
        },
    ];

    private static object[] BuildCanvasBody(CanvasContent canvas) =>
    [
        new Dictionary<string, object>
        {
            ["type"] = "TextBlock",
            ["text"] = canvas.Title,
            ["size"] = "Large",
            ["weight"] = "Bolder",
            ["wrap"] = true,
        },
    ];

    private static object[] BuildFallbackBody() =>
    [
        new Dictionary<string, object>
        {
            ["type"] = "TextBlock",
            ["text"] = "(empty message)",
            ["wrap"] = true,
        },
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };
}
