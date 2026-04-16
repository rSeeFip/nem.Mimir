using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace nem.Lume.McpTools.Tools;

[McpServerToolType]
public static class SaveUserMemoryTool
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	[McpServerTool(Name = "save_user_memory"), Description("Saves a user memory in Lume for later AI context injection.")]
	public static async Task<string> SaveUserMemoryAsync(
		[Description("User identifier associated with the memory.")] string userId,
		[Description("Memory key to persist.")] string key,
		[Description("Memory value to persist.")] string value,
		[Description("Memory category: fact, preference, or context.")] string category,
		IHttpClientFactory httpClientFactory,
		CancellationToken cancellationToken)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(userId))
			{
				return JsonSerializer.Serialize(new { error = "userId is required.", status = "error" }, JsonOptions);
			}

			if (string.IsNullOrWhiteSpace(key))
			{
				return JsonSerializer.Serialize(new { error = "key is required.", status = "error" }, JsonOptions);
			}

			if (string.IsNullOrWhiteSpace(value))
			{
				return JsonSerializer.Serialize(new { error = "value is required.", status = "error" }, JsonOptions);
			}

			if (string.IsNullOrWhiteSpace(category))
			{
				return JsonSerializer.Serialize(new { error = "category is required.", status = "error" }, JsonOptions);
			}

			var client = httpClientFactory.CreateClient("lume-api");
			var payload = new
			{
				userId,
				key,
				value,
				category
			};

			var response = await client.PostAsJsonAsync("/api/v1.0/memory", payload, JsonOptions, cancellationToken).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				return JsonSerializer.Serialize(new
				{
					error = $"Lume API returned {(int)response.StatusCode}: {errorBody}",
					status = "error"
				}, JsonOptions);
			}

			return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			return JsonSerializer.Serialize(new
			{
				error = ex.Message,
				status = "error"
			}, JsonOptions);
		}
	}
}
