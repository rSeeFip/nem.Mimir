using Microsoft.Bot.Schema;
using nem.Contracts.Content;

namespace nem.Mimir.Teams.Services;

internal sealed class ActivityConverter
{
    public IContentPayload Convert(Activity activity)
    {
        var text = activity.Text ?? string.Empty;
        var format = activity.TextFormat?.ToLowerInvariant();
        var createdAt = activity.Timestamp ?? DateTimeOffset.UtcNow;

        return new TextContent(text, format)
        {
            CreatedAt = createdAt,
        };
    }
}
