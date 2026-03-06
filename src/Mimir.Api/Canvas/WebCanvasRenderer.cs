namespace Mimir.Api.Canvas;

using System.Text;
using nem.Contracts.Canvas;

/// <summary>
/// Renders <see cref="ContentBlock"/> arrays to semantic HTML with Tailwind CSS classes.
/// Implements <see cref="ICanvasRenderer"/> with <see cref="RenderFormat.Html"/>.
/// All user-supplied content is HTML-encoded for XSS prevention (G13).
/// No JavaScript is emitted (G5). Forms are display-only (G14).
/// </summary>
public sealed class WebCanvasRenderer : HtmlBlockRenderer, ICanvasRenderer
{
    /// <inheritdoc />
    public RenderFormat Format => RenderFormat.Html;

    /// <inheritdoc />
    public RenderedOutput Render(IReadOnlyList<ContentBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        if (blocks.Count == 0)
            return new RenderedOutput(RenderFormat.Html, string.Empty);

        // Validate all blocks before rendering — throw on invalid
        var validation = ContentBlockValidator.ValidateAll(blocks);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                $"Content blocks failed validation: {string.Join("; ", validation.Errors)}");
        }

        var sb = new StringBuilder(blocks.Count * 128);

        foreach (var block in blocks)
        {
            switch (block)
            {
                case TextBlock tb:
                    RenderTextBlock(sb, tb);
                    break;
                case CodeBlock cb:
                    RenderCodeBlock(sb, cb);
                    break;
                case ImageBlock ib:
                    RenderImageBlock(sb, ib);
                    break;
                case TableBlock tab:
                    RenderTableBlock(sb, tab);
                    break;
                case ChartBlock chb:
                    RenderChartBlock(sb, chb);
                    break;
                case FormBlock fb:
                    RenderFormBlock(sb, fb);
                    break;
                case ProgressBlock pb:
                    RenderProgressBlock(sb, pb);
                    break;
                case LinkBlock lb:
                    RenderLinkBlock(sb, lb);
                    break;
                case DividerBlock:
                    RenderDividerBlock(sb);
                    break;
                default:
                    throw new ArgumentException($"Unknown ContentBlock type: {block.GetType().Name}");
            }
        }

        return new RenderedOutput(RenderFormat.Html, sb.ToString());
    }
}
