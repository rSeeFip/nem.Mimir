using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using nem.Mimir.Domain.Plugins;

namespace nem.Mimir.Infrastructure.Plugins.BuiltIn;

/// <summary>
/// Marketplace plugin that discovers published skills as MCP-compatible tools and proxies execution to nem.Skills.
/// </summary>
internal sealed class SkillMarketplacePlugin : IBuiltInPlugin
{
    internal const string SkillsClientName = "SkillsMarketplace";
    private const string DescriptorCacheKey = "marketplace.skill.descriptors";

    private static readonly TimeSpan DescriptorCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SkillMarketplacePlugin> _logger;

    public SkillMarketplacePlugin(IServiceScopeFactory scopeFactory, ILogger<SkillMarketplacePlugin> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string Id => "mimir.marketplace.skills";
    public string Name => "Marketplace Skills";
    public string Version => "1.0.0";
    public string Description => "Discovers published marketplace skills and delegates execution to nem.Skills service.";

    public async Task<PluginResult> ExecuteAsync(PluginContext context, CancellationToken ct = default)
    {
        if (!TryGetRequiredString(context.Parameters, "skillId", out var skillId, out var missingError))
            return PluginResult.Failure(missingError!);

        if (!TryGetRequiredJson(context.Parameters, "parameters", out var parametersPayload, out var payloadError))
            return PluginResult.Failure(payloadError!);

        var version = TryGetOptionalString(context.Parameters, "version");

        using var scope = _scopeFactory.CreateScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var memoryCache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

        var client = clientFactory.CreateClient(SkillsClientName);
        if (!IsClientConfigured(client))
        {
            return PluginResult.Failure("Skills marketplace is not available. The skills service base URL is not configured.");
        }

        IReadOnlyList<SkillToolDescriptorDto> availableDescriptors;
        try
        {
            availableDescriptors = await GetOrRefreshDescriptorsAsync(client, memoryCache, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to refresh marketplace skill descriptors before executing {SkillId}", skillId);
            return PluginResult.Failure("Skills marketplace is temporarily unavailable.");
        }

        if (!availableDescriptors.Any(descriptor => string.Equals(descriptor.Name, skillId, StringComparison.OrdinalIgnoreCase)))
        {
            return PluginResult.Failure($"Skill '{skillId}' is not available in marketplace descriptors.");
        }

        var escapedSkillId = Uri.EscapeDataString(skillId!);
        var endpoint = string.IsNullOrWhiteSpace(version)
            ? $"api/skills/{escapedSkillId}/execute"
            : $"api/skills/{escapedSkillId}/versions/{Uri.EscapeDataString(version)}/execute";

        using var response = await client.PostAsJsonAsync(
            endpoint,
            new SkillExecutionRequest(
                Code: parametersPayload,
                Language: "json",
                TimeoutSeconds: null),
            cancellationToken: ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return PluginResult.Failure($"Skills execution failed ({(int)response.StatusCode}): {errorBody}");
        }

        using var resultDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);

        return PluginResult.Success(new Dictionary<string, object>
        {
            ["skillId"] = skillId!,
            ["version"] = version ?? string.Empty,
            ["result"] = resultDocument.RootElement.Clone(),
        });
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var memoryCache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

        var client = clientFactory.CreateClient(SkillsClientName);
        if (!IsClientConfigured(client))
        {
            _logger.LogWarning("Skills marketplace client is not configured; descriptor warmup skipped");
            return;
        }

        try
        {
            await RefreshDescriptorsAsync(client, memoryCache, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Skills marketplace descriptor warmup failed; built-in plugin will stay available and retry lazily");
        }
    }

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task InvalidateDescriptorsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var memoryCache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        memoryCache.Remove(DescriptorCacheKey);
        _logger.LogInformation("Marketplace skill descriptor cache invalidated");
        return Task.CompletedTask;
    }

    private static async Task<IReadOnlyList<SkillToolDescriptorDto>> GetOrRefreshDescriptorsAsync(
        HttpClient client,
        IMemoryCache memoryCache,
        CancellationToken ct)
    {
        if (memoryCache.TryGetValue<IReadOnlyList<SkillToolDescriptorDto>>(DescriptorCacheKey, out var cached)
            && cached is not null)
        {
            return cached;
        }

        return await RefreshDescriptorsAsync(client, memoryCache, ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<SkillToolDescriptorDto>> RefreshDescriptorsAsync(
        HttpClient client,
        IMemoryCache memoryCache,
        CancellationToken ct)
    {
        using var response = await client.GetAsync("api/skills?pageNumber=1&pageSize=500&status=Published", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var resultDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
        var itemsElement = resultDocument.RootElement.GetProperty("items");

        var descriptors = new List<SkillToolDescriptorDto>();
        foreach (var item in itemsElement.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
                continue;

            var descriptorResponse = await client.GetAsync($"api/skills/{Uri.EscapeDataString(idElement.GetString()!)}/mcp-descriptor", ct).ConfigureAwait(false);
            if (!descriptorResponse.IsSuccessStatusCode)
                continue;

            var descriptor = await descriptorResponse.Content.ReadFromJsonAsync<SkillToolDescriptorDto>(JsonOptions, ct).ConfigureAwait(false);
            if (descriptor is not null)
                descriptors.Add(descriptor);
        }

        var readOnly = descriptors.AsReadOnly();
        memoryCache.Set(DescriptorCacheKey, readOnly, DescriptorCacheTtl);
        return readOnly;
    }

    private static bool IsClientConfigured(HttpClient client) => client.BaseAddress is not null;

    private static bool TryGetRequiredString(
        IReadOnlyDictionary<string, object> parameters,
        string key,
        out string? value,
        out string? error)
    {
        value = null;
        error = null;

        if (!parameters.TryGetValue(key, out var raw) || raw is null)
        {
            error = $"Required parameter '{key}' is missing.";
            return false;
        }

        if (raw is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            var jsonString = jsonElement.GetString();
            if (!string.IsNullOrWhiteSpace(jsonString))
            {
                value = jsonString;
                return true;
            }
        }

        if (raw is string text && !string.IsNullOrWhiteSpace(text))
        {
            value = text;
            return true;
        }

        error = $"Required parameter '{key}' is missing or empty.";
        return false;
    }

    private static bool TryGetRequiredJson(
        IReadOnlyDictionary<string, object> parameters,
        string key,
        out string value,
        out string? error)
    {
        value = string.Empty;
        error = null;

        if (!parameters.TryGetValue(key, out var raw) || raw is null)
        {
            error = $"Required parameter '{key}' is missing.";
            return false;
        }

        switch (raw)
        {
            case string text when !string.IsNullOrWhiteSpace(text):
                value = text;
                return true;
            case JsonElement element:
                value = element.GetRawText();
                return true;
            default:
                value = JsonSerializer.Serialize(raw, JsonOptions);
                return true;
        }
    }

    private static string? TryGetOptionalString(IReadOnlyDictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var raw) || raw is null)
            return null;

        return raw switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString(),
            _ => raw.ToString(),
        };
    }

    private sealed record SkillExecutionRequest(string Code, string Language, int? TimeoutSeconds);
}
