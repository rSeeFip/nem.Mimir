using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace nem.Lume.McpTools.Tools;

[McpServerToolType]
public static class LumeCreateArticleTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "lume_create_article"), Description("Creates a knowledge base article in a Lume KB space.")]
    public static async Task<string> CreateArticleAsync(
        [Description("Knowledge base space identifier.")] string spaceId,
        [Description("Article title.")] string title,
        [Description("Article content in plain text or markdown.")] string content,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("lume-api");

            var body = new CreateArticleBody(title, content);
            using var response = await client
                .PostAsJsonAsync($"/api/lume/kb/spaces/{spaceId}/articles", body, JsonOptions, cancellationToken)
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

    private sealed record CreateArticleBody(string Title, string Content);
}
