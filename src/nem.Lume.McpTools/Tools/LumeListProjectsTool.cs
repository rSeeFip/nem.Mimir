using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace nem.Lume.McpTools.Tools;

[McpServerToolType]
public static class LumeListProjectsTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "lume_list_projects"), Description("Lists projects in a Lume workspace.")]
    public static async Task<string> ListProjectsAsync(
        [Description("Workspace identifier.")] string workspaceId,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("lume-api");
            var url = $"/api/lume/workspaces/{workspaceId}/projects";
            var response = await client.GetAsync(url, cancellationToken);

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
