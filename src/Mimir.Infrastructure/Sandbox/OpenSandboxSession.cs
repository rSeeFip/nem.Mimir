namespace Mimir.Infrastructure.Sandbox;

using System.Net.Http.Json;
using System.Text.Json;
using nem.Contracts.Sandbox;

public sealed class OpenSandboxSession : ISandboxSession, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISandboxProvider _provider;
    private readonly IHttpClientFactory _httpClientFactory;
    private int _disposed;

    public OpenSandboxSession(
        string sessionId,
        SandboxConfig config,
        ISandboxProvider provider,
        IHttpClientFactory httpClientFactory)
    {
        SessionId = sessionId;
        Config = config;
        _provider = provider;
        _httpClientFactory = httpClientFactory;
    }

    public string SessionId { get; }

    public SandboxConfig Config { get; }

    public async Task<SandboxExecutionResult> ExecuteAsync(
        string code,
        string language,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        var request = new ExecuteRequest(code, language, (int?)timeout?.TotalSeconds);

        using var response = await _httpClientFactory
            .CreateClient(OpenSandboxProvider.HttpClientName)
            .PostAsJsonAsync($"sessions/{Uri.EscapeDataString(SessionId)}/execute", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var exitCode = ReadInt32(document.RootElement, "exitCode") ?? 0;
        var stdout = ReadString(document.RootElement, "stdout") ?? string.Empty;
        var stderr = ReadString(document.RootElement, "stderr") ?? string.Empty;
        var executionTimeMs = ReadInt64(document.RootElement, "executionTimeMs") ?? 0L;

        return new SandboxExecutionResult(exitCode, stdout, stderr, executionTimeMs);
    }

    public async Task UploadFileAsync(string remotePath, Stream content, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);
        ArgumentNullException.ThrowIfNull(content);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(remotePath), "path");
        form.Add(new StreamContent(content), "file", "upload.bin");

        using var response = await _httpClientFactory
            .CreateClient(OpenSandboxProvider.HttpClientName)
            .PostAsync($"sessions/{Uri.EscapeDataString(SessionId)}/files", form, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    public async Task<Stream> DownloadFileAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);

        var encodedPath = Uri.EscapeDataString(remotePath);
        using var response = await _httpClientFactory
            .CreateClient(OpenSandboxProvider.HttpClientName)
            .GetAsync($"sessions/{Uri.EscapeDataString(SessionId)}/files/{encodedPath}", cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var memory = new MemoryStream();
        await response.Content.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        memory.Position = 0;
        return memory;
    }

    public async Task<string> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);

        using var response = await _httpClientFactory
            .CreateClient(OpenSandboxProvider.HttpClientName)
            .GetAsync($"sessions/{Uri.EscapeDataString(SessionId)}", cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ReadString(document.RootElement, "status") ?? "unknown";
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
            await _provider.DestroySessionAsync(SessionId).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
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

    private static int? ReadInt32(JsonElement element, string propertyName)
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

    private static long? ReadInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.Number && value.TryGetInt64(out var i))
        {
            return i;
        }

        if (value.ValueKind is JsonValueKind.String && long.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private sealed record ExecuteRequest(string Code, string Language, int? TimeoutSeconds);
}
