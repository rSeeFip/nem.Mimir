using System.Text;
using System.Text.Json;
using nem.Contracts.Canvas;

namespace Mimir.Teams.Canvas;

/// <summary>
/// Renders ContentBlock arrays to Adaptive Card JSON v1.5 for Microsoft Teams.
/// Follows the Dictionary&lt;string,object&gt; building pattern from <see cref="Services.AdaptiveCardBuilder"/>.
/// Enforces 28KB payload limit per Teams requirements.
/// </summary>
internal sealed class TeamsCanvasRenderer : ICanvasRenderer
{
    /// <summary>Maximum payload size in bytes (28KB) for Teams Adaptive Cards.</summary>
    internal const int MaxPayloadBytes = 28 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    /// <inheritdoc />
    public RenderFormat Format => RenderFormat.AdaptiveCard;

    /// <inheritdoc />
    public RenderedOutput Render(IReadOnlyList<ContentBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        // Validate all blocks before rendering
        var validation = ContentBlockValidator.ValidateAll(blocks);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                $"Content blocks failed validation: {string.Join("; ", validation.Errors)}",
                nameof(blocks));
        }

        var body = new List<object>();

        foreach (var block in blocks)
        {
            var element = RenderBlock(block);
            if (element is not null)
                body.Add(element);
        }

        var card = BuildCardEnvelope(body);
        var json = JsonSerializer.Serialize(card, SerializerOptions);

        // Enforce 28KB payload limit
        if (Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
        {
            json = TruncateToLimit(body);
        }

        return new RenderedOutput(RenderFormat.AdaptiveCard, json);
    }

    private static Dictionary<string, object> BuildCardEnvelope(List<object> body) => new()
    {
        ["type"] = "AdaptiveCard",
        ["version"] = "1.5",
        ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
        ["body"] = body,
    };

    private static string TruncateToLimit(List<object> body)
    {
        // Progressively remove body elements from the end until under limit
        var truncatedBody = new List<object>(body);

        while (truncatedBody.Count > 0)
        {
            truncatedBody.RemoveAt(truncatedBody.Count - 1);

            var truncatedList = new List<object>(truncatedBody)
            {
                new Dictionary<string, object>
                {
                    ["type"] = "TextBlock",
                    ["text"] = "[Content truncated due to payload size limit]",
                    ["wrap"] = true,
                    ["color"] = "Warning",
                },
            };

            var card = BuildCardEnvelope(truncatedList);
            var json = JsonSerializer.Serialize(card, SerializerOptions);

            if (Encoding.UTF8.GetByteCount(json) <= MaxPayloadBytes)
                return json;
        }

        // Worst case: just the truncation message
        var minimalBody = new List<object>
        {
            new Dictionary<string, object>
            {
                ["type"] = "TextBlock",
                ["text"] = "[Content truncated due to payload size limit]",
                ["wrap"] = true,
                ["color"] = "Warning",
            },
        };

        var minimalCard = BuildCardEnvelope(minimalBody);
        return JsonSerializer.Serialize(minimalCard, SerializerOptions);
    }

    private static object? RenderBlock(ContentBlock block) => block switch
    {
        TextBlock tb => RenderTextBlock(tb),
        CodeBlock cb => RenderCodeBlock(cb),
        ImageBlock ib => RenderImageBlock(ib),
        TableBlock tab => RenderTableBlock(tab),
        ChartBlock chb => RenderChartBlock(chb),
        FormBlock fb => RenderFormBlock(fb),
        ProgressBlock pb => RenderProgressBlock(pb),
        LinkBlock lb => RenderLinkBlock(lb),
        DividerBlock => RenderDividerBlock(),
        _ => null,
    };

    private static Dictionary<string, object> RenderTextBlock(TextBlock block) => new()
    {
        ["type"] = "TextBlock",
        ["text"] = block.Text,
        ["wrap"] = true,
    };

    private static Dictionary<string, object> RenderCodeBlock(CodeBlock block)
    {
        var element = new Dictionary<string, object>
        {
            ["type"] = "TextBlock",
            ["text"] = block.Code,
            ["wrap"] = true,
            ["fontType"] = "Monospace",
        };

        if (block.Title is not null)
        {
            element["separator"] = true;
        }

        return element;
    }

    private static Dictionary<string, object> RenderImageBlock(ImageBlock block)
    {
        var element = new Dictionary<string, object>
        {
            ["type"] = "Image",
            ["url"] = block.Url,
        };

        if (block.AltText is not null)
            element["altText"] = block.AltText;

        if (block.Width is not null || block.Height is not null)
            element["size"] = "Auto";

        return element;
    }

    private static Dictionary<string, object> RenderTableBlock(TableBlock block)
    {
        // AC v1.5 Table element
        var columns = block.Headers.Select(h => new Dictionary<string, object>
        {
            ["width"] = 1,
        }).ToArray();

        var headerCells = block.Headers.Select(h => new Dictionary<string, object>
        {
            ["type"] = "TableCell",
            ["items"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "TextBlock",
                    ["text"] = h,
                    ["weight"] = "Bolder",
                    ["wrap"] = true,
                },
            },
        }).ToArray();

        var headerRow = new Dictionary<string, object>
        {
            ["type"] = "TableRow",
            ["cells"] = headerCells,
        };

        var dataRows = block.Rows.Select(row => new Dictionary<string, object>
        {
            ["type"] = "TableRow",
            ["cells"] = row.Select(cell => new Dictionary<string, object>
            {
                ["type"] = "TableCell",
                ["items"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "TextBlock",
                        ["text"] = cell,
                        ["wrap"] = true,
                    },
                },
            }).ToArray(),
        }).ToArray();

        var rows = new List<object> { headerRow };
        rows.AddRange(dataRows);

        return new Dictionary<string, object>
        {
            ["type"] = "Table",
            ["columns"] = columns,
            ["rows"] = rows.ToArray(),
        };
    }

    private static Dictionary<string, object> RenderChartBlock(ChartBlock block)
    {
        // G5: No JavaScript — render as descriptive text with chart data summary
        var description = block.Title is not null
            ? $"📊 Chart: {block.Title} (Type: {block.ChartType})"
            : $"📊 Chart (Type: {block.ChartType})";

        return new Dictionary<string, object>
        {
            ["type"] = "TextBlock",
            ["text"] = description,
            ["wrap"] = true,
        };
    }

    private static object RenderFormBlock(FormBlock block)
    {
        // G14: Display-only forms — NO Action.Submit
        var items = new List<object>();

        foreach (var field in block.Fields)
        {
            items.Add(RenderFormField(field));
        }

        return new Dictionary<string, object>
        {
            ["type"] = "Container",
            ["items"] = items,
        };
    }

    private static Dictionary<string, object> RenderFormField(FormField field)
    {
        return field.FieldType.ToLowerInvariant() switch
        {
            "text" or "string" => new Dictionary<string, object>
            {
                ["type"] = "Input.Text",
                ["id"] = field.Name,
                ["label"] = field.Label,
                ["isRequired"] = field.Required,
                ["isVisible"] = true,
                ["value"] = field.DefaultValue ?? string.Empty,
            },
            "number" => new Dictionary<string, object>
            {
                ["type"] = "Input.Number",
                ["id"] = field.Name,
                ["label"] = field.Label,
                ["isRequired"] = field.Required,
                ["isVisible"] = true,
            },
            "date" => new Dictionary<string, object>
            {
                ["type"] = "Input.Date",
                ["id"] = field.Name,
                ["label"] = field.Label,
                ["isRequired"] = field.Required,
                ["isVisible"] = true,
            },
            "toggle" or "boolean" => new Dictionary<string, object>
            {
                ["type"] = "Input.Toggle",
                ["id"] = field.Name,
                ["title"] = field.Label,
                ["isVisible"] = true,
            },
            "choice" or "select" => new Dictionary<string, object>
            {
                ["type"] = "Input.ChoiceSet",
                ["id"] = field.Name,
                ["label"] = field.Label,
                ["isRequired"] = field.Required,
                ["isVisible"] = true,
                ["choices"] = Array.Empty<object>(),
            },
            _ => new Dictionary<string, object>
            {
                ["type"] = "Input.Text",
                ["id"] = field.Name,
                ["label"] = field.Label,
                ["isRequired"] = field.Required,
                ["isVisible"] = true,
                ["value"] = field.DefaultValue ?? string.Empty,
            },
        };
    }

    private static Dictionary<string, object> RenderProgressBlock(ProgressBlock block)
    {
        // ASCII progress bar: [████████░░] 75% - Label
        const int barWidth = 20;
        var filledCount = (int)Math.Round(block.Percent / 100.0 * barWidth);
        var emptyCount = barWidth - filledCount;

        var bar = $"[{new string('█', filledCount)}{new string('░', emptyCount)}] {block.Percent:F0}%";

        var text = block.Status is not null
            ? $"{bar} — {block.Label} ({block.Status})"
            : $"{bar} — {block.Label}";

        return new Dictionary<string, object>
        {
            ["type"] = "TextBlock",
            ["text"] = text,
            ["wrap"] = true,
            ["fontType"] = "Monospace",
        };
    }

    private static Dictionary<string, object> RenderLinkBlock(LinkBlock block)
    {
        var markdownLink = block.Title is not null
            ? $"[{block.Text}]({block.Url} \"{block.Title}\")"
            : $"[{block.Text}]({block.Url})";

        return new Dictionary<string, object>
        {
            ["type"] = "TextBlock",
            ["text"] = markdownLink,
            ["wrap"] = true,
        };
    }

    private static Dictionary<string, object> RenderDividerBlock() => new()
    {
        ["type"] = "ColumnSet",
        ["separator"] = true,
        ["columns"] = Array.Empty<object>(),
    };
}
