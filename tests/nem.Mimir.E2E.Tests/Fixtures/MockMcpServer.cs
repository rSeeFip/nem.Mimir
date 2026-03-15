using System.ComponentModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace nem.Mimir.E2E.Tests.Fixtures;

/// <summary>
/// Runs an in-process ASP.NET Core MCP server using the ModelContextProtocol SDK.
/// Exposes a "mock_search" tool over SSE transport for E2E testing.
/// </summary>
public sealed class MockMcpServer : IAsyncDisposable
{
    private WebApplication? _app;

    /// <summary>
    /// The base URL the mock MCP server is listening on (e.g., http://localhost:12345).
    /// </summary>
    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>
    /// The SSE endpoint URL for MCP client connections.
    /// MapMcp("/mcp") registers SSE at /mcp/sse and message endpoint at /mcp/message.
    /// </summary>
    public string SseUrl => $"{BaseUrl}/mcp/sse";

    /// <summary>
    /// The Streamable HTTP endpoint URL for MCP client connections.
    /// </summary>
    public string StreamableHttpUrl => $"{BaseUrl}/mcp";

    /// <summary>
    /// Starts the mock MCP server on a random available port.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // Prevent loading appsettings.json from the test working directory
            // which might contain host filtering or other unwanted configuration.
            ContentRootPath = Path.GetTempPath(),
        });

        // Explicit Kestrel configuration to avoid any HttpSys/IIS interference
        builder.WebHost.UseKestrel();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        // Ensure no host filtering rejects requests to 127.0.0.1
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "*",
        });

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "MockMcpTestServer",
                    Version = "1.0.0",
                };
            })
            .WithHttpTransport()
            .WithTools<MockSearchTool>();

        _app = builder.Build();
        _app.MapMcp("/mcp");

        await _app.StartAsync(ct);

        // Get the actual bound address (port 0 is resolved to a real port by Kestrel)
        var server = _app.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        BaseUrl = addressFeature?.Addresses.FirstOrDefault()
                  ?? _app.Urls.First();
    }

    /// <summary>
    /// Stops and disposes the mock MCP server.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }
}

/// <summary>
/// Mock MCP tool that simulates a search operation.
/// Returns deterministic results for E2E testing.
/// </summary>
[McpServerToolType]
public sealed class MockSearchTool
{
    public const string ToolName = "mock_search";
    public const string ExpectedResult = "Mock search result: Found 3 items for query";

    [McpServerTool(Name = ToolName)]
    [Description("Searches for information based on a query string.")]
    public static string MockSearch(
        [Description("The search query string")] string query)
    {
        return $"{ExpectedResult} '{query}'";
    }
}
