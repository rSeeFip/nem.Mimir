using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using nem.Contracts.Lume;
using ModelContextProtocol.Server;

namespace nem.Lume.McpTools.Tools;

[McpServerToolType]
public static class LumeCreateTaskTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "lume_create_task"), Description("Creates a task in a Lume project board column.")]
    public static async Task<string> CreateTaskAsync(
        [Description("Workspace identifier.")] string workspaceId,
        [Description("Board identifier.")] string boardId,
        [Description("Column identifier.")] string columnId,
        [Description("Task title.")] string title,
        [Description("Optional task description.")] string? description,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!LumeWorkspaceId.TryParse(workspaceId, null, out var wsId))
            {
                return JsonSerializer.Serialize(new { error = "Invalid workspaceId.", status = "error" }, JsonOptions);
            }

            if (!LumeBoardId.TryParse(boardId, null, out var bId))
            {
                return JsonSerializer.Serialize(new { error = "Invalid boardId.", status = "error" }, JsonOptions);
            }

            if (!LumeBoardColumnId.TryParse(columnId, null, out var colId))
            {
                return JsonSerializer.Serialize(new { error = "Invalid columnId.", status = "error" }, JsonOptions);
            }

            var client = httpClientFactory.CreateClient("lume-api");
            var payload = new
            {
                title,
                description,
                workspaceId = wsId.ToString(),
            };

            var url = $"/api/lume/projects/{wsId}/boards/{bId}/columns/{colId}/tasks";
            var response = await client.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Serialize(new
                {
                    error = $"Lume API returned {(int)response.StatusCode}: {errorBody}",
                    status = "error",
                }, JsonOptions);
            }

            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            return result;
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
