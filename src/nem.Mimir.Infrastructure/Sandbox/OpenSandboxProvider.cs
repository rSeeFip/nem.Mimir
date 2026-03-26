namespace nem.Mimir.Infrastructure.Sandbox;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using nem.Contracts.Sandbox;

public sealed class OpenSandboxProvider : ISandboxProvider
{
    public const string HttpClientName = "OpenSandbox";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<OpenSandboxOptions> _options;

    public OpenSandboxProvider(IHttpClientFactory httpClientFactory, IOptionsMonitor<OpenSandboxOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async Task<ISandboxSession> CreateSessionAsync(SandboxConfig config, SandboxPolicyClass policyClass, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var request = new CreateSessionRequest(
            Image: config.Image,
            MemoryLimitMb: config.MemoryLimitMb,
            CpuLimit: config.CpuLimit,
            TimeoutSeconds: config.TimeoutSeconds,
            IsolationLevel: config.IsolationLevel.ToString(),
            NetworkPolicy: config.NetworkPolicy,
            PolicyClass: policyClass.ToString());

        using var response = await _httpClientFactory
            .CreateClient(HttpClientName)
            .PostAsJsonAsync("sessions", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var sessionId = await ReadSessionIdAsync(response, cancellationToken).ConfigureAwait(false);
        return new OpenSandboxSession(sessionId, config, this, _httpClientFactory);
    }

    public async Task DestroySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        using var response = await _httpClientFactory
            .CreateClient(HttpClientName)
            .DeleteAsync($"sessions/{Uri.EscapeDataString(sessionId)}", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ISandboxSession>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClientFactory
            .CreateClient(HttpClientName)
            .GetAsync("sessions", cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var list = new List<ISandboxSession>();
        var options = _options.CurrentValue;

        if (document.RootElement.ValueKind is JsonValueKind.Array)
        {
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var id = TryGetString(element, "id") ?? TryGetString(element, "sessionId");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var image = TryGetString(element, "image") ?? options.DefaultImage;
                var timeoutSeconds = TryGetInt32(element, "timeoutSeconds") ?? 300;
                var memoryLimitMb = TryGetInt32(element, "memoryLimitMb") ?? 512;
                var cpuLimit = TryGetDouble(element, "cpuLimit") ?? 1.0;
                var networkPolicy = TryGetString(element, "networkPolicy") ?? "none";

                var config = new SandboxConfig(
                    Image: image,
                    MemoryLimitMb: memoryLimitMb,
                    CpuLimit: cpuLimit,
                    TimeoutSeconds: timeoutSeconds,
                    IsolationLevel: SandboxIsolationLevel.gVisor,
                    NetworkPolicy: networkPolicy);

                list.Add(new OpenSandboxSession(id, config, this, _httpClientFactory));
            }
        }

        return list;
    }

    public async Task<bool> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClientFactory
            .CreateClient(HttpClientName)
            .GetAsync("health", cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        if (response.Content.Headers.ContentLength is 0)
        {
            return true;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (document.RootElement.ValueKind is JsonValueKind.Object)
        {
            var healthy = TryGetBoolean(document.RootElement, "healthy");
            if (healthy.HasValue)
            {
                return healthy.Value;
            }

            var status = TryGetString(document.RootElement, "status");
            if (!string.IsNullOrWhiteSpace(status))
            {
                return status.Equals("ok", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("healthy", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("up", StringComparison.OrdinalIgnoreCase);
            }
        }

        return true;
    }

    private static async Task<string> ReadSessionIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var sessionId = TryGetString(document.RootElement, "sessionId")
            ?? TryGetString(document.RootElement, "id");

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException("OpenSandbox create session response did not contain sessionId.");
        }

        return sessionId;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.Number && value.TryGetInt32(out var i))
        {
            return i;
        }

        if (value.ValueKind is JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static double? TryGetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.Number && value.TryGetDouble(out var d))
        {
            return d;
        }

        if (value.ValueKind is JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool? TryGetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind is JsonValueKind.False)
        {
            return false;
        }

        if (value.ValueKind is JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private sealed record CreateSessionRequest(
        string Image,
        int MemoryLimitMb,
        double CpuLimit,
        int TimeoutSeconds,
        string IsolationLevel,
        string NetworkPolicy,
        string PolicyClass);
}
