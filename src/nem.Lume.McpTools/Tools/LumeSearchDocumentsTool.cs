using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace nem.Lume.McpTools.Tools;

[McpServerToolType]
public static class LumeSearchDocumentsTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "lume_search_documents"), Description("Searches Lume documents using RAG (Retrieval-Augmented Generation).")]
    public static async Task<string> SearchDocumentsAsync(
        [Description("The search query text.")] string query,
        [Description("Workspace identifier to scope the search.")] string workspaceId,
        [Description("Maximum number of results to return (1-50, default 10).")] int? topK,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("lume-api");

            var body = new RagSearchBody(query, workspaceId, topK ?? 10);
            using var response = await client
                .PostAsJsonAsync("/api/lume/search/rag", body, JsonOptions, cancellationToken)
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

    private sealed record RagSearchBody(string Query, string WorkspaceId, int TopK);
}
