using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace nem.Lume.McpTools.Tools;

[McpServerToolType]
public static class LumeMoveTaskTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "lume_move_task"), Description("Moves a Lume task to a different column.")]
    public static async Task<string> MoveTaskAsync(
        [Description("Task identifier.")] string taskId,
        [Description("Target column identifier.")] string targetColumnId,
        [Description("Optional position within the column (0-based).")] int? position,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("lume-api");
            var payload = new
            {
                targetColumnId,
                position,
            };

            var url = $"/api/lume/tasks/{taskId}/move";
            var response = await client.PutAsJsonAsync(url, payload, JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Serialize(new
                {
                    error = $"Lume API returned {(int)response.StatusCode}: {errorBody}",
                    status = "error",
                }, JsonOptions);
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                status = "error",
            }, JsonOptions);
        }
    }
}
