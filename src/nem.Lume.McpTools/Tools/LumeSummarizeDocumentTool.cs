using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace nem.Lume.McpTools.Tools;

[McpServerToolType]
public static class LumeSummarizeDocumentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "lume_summarize_document"), Description("Generates a summary of a Lume document.")]
    public static async Task<string> SummarizeDocumentAsync(
        [Description("Document identifier to summarize.")] string documentId,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("lume-api");

            using var response = await client
                .PostAsJsonAsync($"/api/lume/documents/{documentId}/summarize", new { }, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Serialize(
                    new { error = $"Lume API returned {(int)response.StatusCode}: {detail}", status = "error" },
                    JsonOptions);
            }

            var result = await response.Content
                .ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message, status = "error" }, JsonOptions);
        }
    }
}
