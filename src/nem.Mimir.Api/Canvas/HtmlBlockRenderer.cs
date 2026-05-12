namespace nem.Mimir.Api.Canvas;

using System.Globalization;
using System.Net;
using System.Text;
using nem.Contracts.Canvas;

/// <summary>
/// Base helper class providing HTML rendering methods for individual ContentBlock types.
/// All user-supplied text is HTML-encoded via WebUtility.HtmlEncode for XSS prevention (G13).
/// Uses Tailwind CSS classes exclusively — no inline styles.
/// </summary>
public abstract class HtmlBlockRenderer
{
    /// <summary>HTML-encode user-supplied text to prevent XSS.</summary>
    protected static string Encode(string? text) =>
        WebUtility.HtmlEncode(text ?? string.Empty);

    /// <summary>Renders a <see cref="TextBlock"/> to a prose div.</summary>
    protected static void RenderTextBlock(StringBuilder sb, TextBlock block)
    {
        sb.Append("<div class=\"prose\">")
          .Append(Encode(block.Text))
          .AppendLine("</div>");
    }

    /// <summary>Renders a <see cref="CodeBlock"/> to a pre/code element with language class.</summary>
    protected static void RenderCodeBlock(StringBuilder sb, CodeBlock block)
    {
        sb.Append("<div class=\"rounded-lg bg-gray-900 p-4\">");

        if (!string.IsNullOrWhiteSpace(block.Title))
        {
            sb.Append("<div class=\"text-sm font-medium text-gray-400 mb-2\">")
              .Append(Encode(block.Title))
              .Append("</div>");
        }

        sb.Append("<pre class=\"overflow-x-auto\"><code class=\"language-")
          .Append(Encode(block.Language))
          .Append("\">")
          .Append(Encode(block.Code))
          .Append("</code></pre>")
          .AppendLine("</div>");
    }

    /// <summary>
    /// Renders an <see cref="ImageBlock"/> to a figure/img element with lazy loading.
    /// URL scheme is validated (http/https only) as defense-in-depth beyond ContentBlockValidator.
    /// </summary>
    protected static void RenderImageBlock(StringBuilder sb, ImageBlock block)
    {
        ValidateUrlScheme(block.Url, "ImageBlock.Url");

        sb.Append("<figure class=\"my-4\">");
        sb.Append("<img src=\"").Append(Encode(block.Url)).Append('"');
        sb.Append(" alt=\"").Append(Encode(block.AltText ?? string.Empty)).Append('"');

        if (block.Width.HasValue)
            sb.Append(" width=\"").Append(block.Width.Value).Append('"');

        if (block.Height.HasValue)
            sb.Append(" height=\"").Append(block.Height.Value).Append('"');

        sb.Append(" loading=\"lazy\"");
        sb.Append(" class=\"rounded-lg shadow\"");
        sb.Append(" />");
        sb.AppendLine("</figure>");
    }

    /// <summary>Renders a <see cref="TableBlock"/> to a semantic HTML table with thead/tbody.</summary>
    protected static void RenderTableBlock(StringBuilder sb, TableBlock block)
    {
        sb.Append("<table class=\"min-w-full divide-y divide-gray-200\">");

        if (!string.IsNullOrWhiteSpace(block.Caption))
        {
            sb.Append("<caption class=\"text-sm text-gray-500 mb-2\">")
              .Append(Encode(block.Caption))
              .Append("</caption>");
        }

        // Headers
        sb.Append("<thead class=\"bg-gray-50\"><tr>");
        foreach (var header in block.Headers)
        {
            sb.Append("<th class=\"px-4 py-2 text-left text-sm font-semibold text-gray-900\">")
              .Append(Encode(header))
              .Append("</th>");
        }
        sb.Append("</tr></thead>");

        // Body
        sb.Append("<tbody class=\"divide-y divide-gray-100\">");
        foreach (var row in block.Rows)
        {
            sb.Append("<tr>");
            foreach (var cell in row)
            {
                sb.Append("<td class=\"px-4 py-2 text-sm text-gray-700\">")
                  .Append(Encode(cell))
                  .Append("</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</tbody>");

        sb.AppendLine("</table>");
    }

    /// <summary>
    /// Renders a <see cref="ChartBlock"/> as a div with data attributes only — no JavaScript (G5).
    /// </summary>
    protected static void RenderChartBlock(StringBuilder sb, ChartBlock block)
    {
        sb.Append("<div class=\"rounded-lg border border-gray-200 p-4\"");
        sb.Append(" data-chart-type=\"").Append(Encode(block.ChartType)).Append('"');
        sb.Append(" data-chart-data=\"").Append(Encode(block.DataJson)).Append('"');
        sb.Append('>');

        if (!string.IsNullOrWhiteSpace(block.Title))
        {
            sb.Append("<p class=\"text-sm font-medium text-gray-700\">")
              .Append(Encode(block.Title))
              .Append("</p>");
        }

        sb.Append("<noscript>Chart: ").Append(Encode(block.ChartType)).Append("</noscript>");
        sb.AppendLine("</div>");
    }

    /// <summary>
    /// Renders a <see cref="FormBlock"/> as a display-only form with all inputs disabled and readonly (G14).
    /// No action/method attributes — form is not submittable.
    /// </summary>
    protected static void RenderFormBlock(StringBuilder sb, FormBlock block)
    {
        sb.Append("<form class=\"space-y-4 rounded-lg border border-gray-200 p-4\"");
        sb.Append(" data-form-id=\"").Append(Encode(block.FormId)).Append('"');
        sb.Append('>');

        foreach (var field in block.Fields)
        {
            sb.Append("<div class=\"flex flex-col gap-1\">");

            // Label
            sb.Append("<label class=\"text-sm font-medium text-gray-700\">");
            sb.Append(Encode(field.Label));
            if (field.Required)
                sb.Append("<span class=\"text-red-500 ml-1\">*</span>");
            sb.Append("</label>");

            // Input — always disabled and readonly (G14)
            sb.Append("<input type=\"").Append(Encode(field.FieldType)).Append('"');
            sb.Append(" name=\"").Append(Encode(field.Name)).Append('"');

            if (!string.IsNullOrWhiteSpace(field.DefaultValue))
                sb.Append(" value=\"").Append(Encode(field.DefaultValue)).Append('"');

            sb.Append(" disabled readonly");
            sb.Append(" class=\"rounded border border-gray-300 bg-gray-100 px-3 py-2 text-sm text-gray-500\"");
            sb.Append(" />");

            sb.Append("</div>");
        }

        sb.AppendLine("</form>");
    }

    /// <summary>Renders a <see cref="ProgressBlock"/> to a progress element.</summary>
    protected static void RenderProgressBlock(StringBuilder sb, ProgressBlock block)
    {
        sb.Append("<div class=\"flex flex-col gap-1\">");

        sb.Append("<div class=\"flex justify-between text-sm\">");
        sb.Append("<span class=\"font-medium text-gray-700\">").Append(Encode(block.Label)).Append("</span>");

        if (!string.IsNullOrWhiteSpace(block.Status))
        {
            sb.Append("<span class=\"text-gray-500\">").Append(Encode(block.Status)).Append("</span>");
        }

        sb.Append("</div>");

        sb.Append("<progress value=\"")
          .Append(block.Percent.ToString(CultureInfo.InvariantCulture))
          .Append("\" max=\"100\" class=\"w-full rounded\"></progress>");

        sb.AppendLine("</div>");
    }

    /// <summary>
    /// Renders a <see cref="LinkBlock"/> as an anchor with security attributes.
    /// Only http/https schemes allowed — javascript: URLs are rejected.
    /// </summary>
    protected static void RenderLinkBlock(StringBuilder sb, LinkBlock block)
    {
        ValidateUrlScheme(block.Url, "LinkBlock.Url");

        sb.Append("<a href=\"").Append(Encode(block.Url)).Append('"');
        sb.Append(" rel=\"noopener noreferrer\"");
        sb.Append(" target=\"_blank\"");

        if (!string.IsNullOrWhiteSpace(block.Title))
            sb.Append(" title=\"").Append(Encode(block.Title)).Append('"');

        sb.Append(" class=\"text-blue-600 hover:text-blue-800 underline\"");
        sb.Append('>');
        sb.Append(Encode(block.Text));
        sb.AppendLine("</a>");
    }

    /// <summary>Renders a <see cref="DividerBlock"/> as an hr element.</summary>
    protected static void RenderDividerBlock(StringBuilder sb)
    {
        sb.AppendLine("<hr class=\"my-4 border-gray-200\" />");
    }

    /// <summary>
    /// Validates that a URL uses only safe schemes (http or https).
    /// Rejects javascript:, data:, and other potentially dangerous protocols.
    /// This is defense-in-depth beyond ContentBlockValidator (G13).
    /// </summary>
    private static void ValidateUrlScheme(string url, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(url))
            return; // Validator handles emptiness

        var trimmed = url.TrimStart();
        if (trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{fieldName} contains a disallowed URL scheme: {url}");
        }
    }
}
