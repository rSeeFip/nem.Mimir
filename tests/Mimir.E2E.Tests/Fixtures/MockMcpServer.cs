using System.ComponentModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Mimir.E2E.Tests.Fixtures;

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
    /// </summary>
    public string SseUrl => $"{BaseUrl}/sse";

    /// <summary>
    /// Starts the mock MCP server on a random available port.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

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
        _app.MapMcp();

        await _app.StartAsync(ct);

        var address = _app.Urls.First();
        BaseUrl = address;
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
