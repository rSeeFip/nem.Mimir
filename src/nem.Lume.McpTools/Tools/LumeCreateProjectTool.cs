using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace nem.Lume.McpTools.Tools;

[McpServerToolType]
public static class LumeCreateProjectTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "lume_create_project"), Description("Creates a project in a Lume workspace.")]
    public static async Task<string> CreateProjectAsync(
        [Description("Workspace identifier.")] string workspaceId,
        [Description("Project name.")] string name,
        [Description("Optional project description.")] string? description,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("lume-api");
            var payload = new
            {
                name,
                description,
            };

            var url = $"/api/lume/workspaces/{workspaceId}/projects";
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
