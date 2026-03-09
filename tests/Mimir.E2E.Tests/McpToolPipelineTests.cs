using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mimir.E2E.Tests.Fixtures;
using Mimir.E2E.Tests.Helpers;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Mimir.E2E.Tests;

/// <summary>
/// End-to-end tests for the full MCP tool call pipeline:
/// configure MCP server, whitelist tools, send a chat message that triggers tool calling,
/// and verify the tool loop + audit log.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class McpToolPipelineTests : IAsyncLifetime
{
    private readonly E2EWebApplicationFactory _factory;
    private MockMcpServer? _mockMcpServer;

    public McpToolPipelineTests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        _mockMcpServer = new MockMcpServer();
        await _mockMcpServer.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_mockMcpServer is not null)
        {
            await _mockMcpServer.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates an HttpClient with a JWT token that includes the "admin" role,
    /// required for MCP server management endpoints.
    /// </summary>
    private HttpClient CreateAdminClient(string? userId = null)
    {
        userId ??= Guid.NewGuid().ToString();
        var client = _factory.CreateClient();
        var token = JwtTokenHelper.GenerateToken(userId, roles: ["admin"]);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Creates an HttpClient with a valid JWT token (no admin role).
    /// </summary>
    private HttpClient CreateUserClient(string? userId = null)
    {
        return _factory.CreateAuthenticatedClient(userId);
    }

    /// <summary>
    /// Registers the mock MCP server via the admin API, enables it, whitelists the tool,
    /// and connects the MCP client directly (bypassing the background polling service).
    /// Returns the server config ID.
    /// </summary>
    private async Task<Guid> RegisterAndConnectMockMcpServerAsync(
        HttpClient adminClient,
        CancellationToken ct = default)
    {
        // 1. Create the MCP server config (enabled from the start)
        var createResponse = await adminClient.PostAsJsonAsync("/api/mcp/servers", new
        {
            name = $"MockMcpTestServer-{Guid.NewGuid():N}",
            transportType = 2, // StreamableHttp
            url = _mockMcpServer!.StreamableHttpUrl,
            isEnabled = true,
            description = "E2E test mock MCP server",
        }, ct);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"Failed to create MCP server config: {await createResponse.Content.ReadAsStringAsync(ct)}");

        var serverId = await createResponse.Content.ReadFromJsonAsync<Guid>(ct);
        serverId.ShouldNotBe(Guid.Empty);

        // 2. Directly connect the MCP client manager to the mock server
        //    This bypasses the background polling service (McpConfigChangeListener)
        //    which has unpredictable timing in E2E tests.
        var clientManager = _factory.Services.GetRequiredService<Mimir.Domain.McpServers.IMcpClientManager>();
        var config = new Mimir.Domain.McpServers.McpServerConfig
        {
            Id = serverId,
            Name = $"MockMcpTestServer",
            TransportType = Mimir.Domain.McpServers.McpTransportType.StreamableHttp,
            Url = _mockMcpServer.StreamableHttpUrl,
            IsEnabled = true,
        };
        await clientManager.ConnectAsync(config, ct);

        // Verify the connection is healthy and tools are discovered
        var tools = await clientManager.GetServerToolsAsync(serverId, ct);
        tools.ShouldNotBeEmpty(
            $"MockMcpServer should expose at least one tool. " +
            $"URL: {_mockMcpServer.StreamableHttpUrl}");

        // 3. Whitelist the mock_search tool
        var whitelistResponse = await adminClient.PutAsJsonAsync(
            $"/api/mcp/servers/{serverId}/whitelist",
            new
            {
                entries = new[]
                {
                    new { toolName = MockSearchTool.ToolName, isEnabled = true },
                },
            }, ct);

        whitelistResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent,
            $"Failed to whitelist tool: {await whitelistResponse.Content.ReadAsStringAsync(ct)}");

        return serverId;
    }

    /// <summary>
    /// Creates a conversation for the given user and returns its ID.
    /// </summary>
    private async Task<Guid> CreateConversationAsync(HttpClient userClient, CancellationToken ct = default)
    {
        var response = await userClient.PostAsJsonAsync("/api/conversations", new
        {
            title = $"MCP Tool Test {Guid.NewGuid():N}",
            model = "qwen-2.5-72b",
        }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.GetProperty("id").GetGuid();
        id.ShouldNotBe(Guid.Empty);
        return id;
    }

    /// <summary>
    /// Configures WireMock with callback-based stateful behavior:
    /// - First POST to /v1/chat/completions returns a tool_call response
    /// - Second POST returns a normal text response
    /// Uses a thread-safe counter instead of WireMock scenarios (which have known
    /// issues with state initialization after Reset() in WireMock.Net).
    /// </summary>
    private void ConfigureWireMockForToolCallScenario(
        string toolName = "mock_search",
        string toolArguments = """{"query":"test query"}""",
        string finalResponseText = "Based on the search results, I found 3 items.")
    {
        _factory.WireMock.Reset();

        // Re-add health endpoint after reset
        _factory.WireMock
            .Given(Request.Create()
                .WithPath("/health")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"status":"healthy"}"""));

        // Re-add models endpoint after reset
        _factory.WireMock
            .Given(Request.Create()
                .WithPath("/v1/models")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                        "object": "list",
                        "data": [
                            {"id": "qwen-2.5-72b", "object": "model", "created": 1234567890, "owned_by": "litellm"}
                        ]
                    }
                    """));

        // Build the two response bodies
        var toolCallBody = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-tool1",
            @object = "chat.completion",
            model = "qwen-2.5-72b",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = (string?)null,
                        tool_calls = new[]
                        {
                            new
                            {
                                id = "call_e2e_001",
                                type = "function",
                                function = new
                                {
                                    name = toolName,
                                    arguments = toolArguments,
                                },
                            },
                        },
                    },
                    finish_reason = "tool_calls",
                },
            },
            usage = new { prompt_tokens = 50, completion_tokens = 20, total_tokens = 70 },
        });

        var textBody = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-final",
            @object = "chat.completion",
            model = "qwen-2.5-72b",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = finalResponseText,
                        tool_calls = (object?)null,
                    },
                    finish_reason = "stop",
                },
            },
            usage = new { prompt_tokens = 100, completion_tokens = 30, total_tokens = 130 },
        });

        // Use callback with counter: first call returns tool_call, subsequent calls return text
        var callCount = 0;
        _factory.WireMock
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(req =>
                {
                    var count = Interlocked.Increment(ref callCount);
                    return count == 1 ? toolCallBody : textBody;
                }));
    }

    /// <summary>
    /// Configures WireMock to always return tool_call responses for the first 5 calls,
    /// then a text response on the 6th call. Used to test the max iterations safety limit.
    /// Uses a thread-safe counter instead of WireMock scenarios.
    /// </summary>
    private void ConfigureWireMockForInfiniteToolCalls(
        string toolName = "mock_search",
        string toolArguments = """{"query":"infinite loop test"}""",
        string finalFallbackText = "I exceeded the maximum number of tool iterations.")
    {
        _factory.WireMock.Reset();

        // Re-add health and models endpoints
        _factory.WireMock
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"status":"healthy"}"""));

        _factory.WireMock
            .Given(Request.Create().WithPath("/v1/models").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"object":"list","data":[{"id":"qwen-2.5-72b","object":"model","created":1234567890,"owned_by":"litellm"}]}"""));

        var finalTextBody = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-final-fallback",
            @object = "chat.completion",
            model = "qwen-2.5-72b",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = finalFallbackText,
                        tool_calls = (object?)null,
                    },
                    finish_reason = "stop",
                },
            },
            usage = new { prompt_tokens = 200, completion_tokens = 50, total_tokens = 250 },
        });

        // The handler makes MaxToolIterations (5) calls with tools, then 1 final call without tools.
        // We need to provide tool_call responses for the first 5 calls and a text response for the 6th.
        var callCount = 0;
        _factory.WireMock
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(req =>
                {
                    var count = Interlocked.Increment(ref callCount);
                    if (count <= 5)
                    {
                        return JsonSerializer.Serialize(new
                        {
                            id = $"chatcmpl-iter{count}",
                            @object = "chat.completion",
                            model = "qwen-2.5-72b",
                            choices = new[]
                            {
                                new
                                {
                                    index = 0,
                                    message = new
                                    {
                                        role = "assistant",
                                        content = (string?)null,
                                        tool_calls = new[]
                                        {
                                            new
                                            {
                                                id = $"call_iter_{count}",
                                                type = "function",
                                                function = new
                                                {
                                                    name = toolName,
                                                    arguments = toolArguments,
                                                },
                                            },
                                        },
                                    },
                                    finish_reason = "tool_calls",
                                },
                            },
                            usage = new { prompt_tokens = 50, completion_tokens = 20, total_tokens = 70 },
                        });
                    }

                    return finalTextBody;
                }));
    }

    /// <summary>
    /// Configures WireMock to return a simple non-streaming text response (no tool calls).
    /// Used for tests where we just need the LLM to respond without calling tools.
    /// </summary>
    private void ConfigureWireMockForSimpleTextResponse(string responseText = "Hello from Mimir")
    {
        _factory.WireMock.Reset();

        _factory.WireMock
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"status":"healthy"}"""));

        _factory.WireMock
            .Given(Request.Create().WithPath("/v1/models").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"object":"list","data":[{"id":"qwen-2.5-72b","object":"model","created":1234567890,"owned_by":"litellm"}]}"""));

        _factory.WireMock
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    id = "chatcmpl-simple",
                    @object = "chat.completion",
                    model = "qwen-2.5-72b",
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            message = new { role = "assistant", content = responseText },
                            finish_reason = "stop",
                        },
                    },
                    usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 },
                })));
    }

    // ──────────────────────────────────────────────────────────────
    // Test 1: Happy path — tool call and response
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_ToolCallAndResponse_ReturnsAssistantResponseWithToolResult()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var adminClient = CreateAdminClient(userId);
        var userClient = CreateUserClient(userId);

        // Register mock MCP server and connect
        var serverId = await RegisterAndConnectMockMcpServerAsync(adminClient);

        // Create a conversation
        var conversationId = await CreateConversationAsync(userClient);

        // Configure WireMock: first call returns tool_call, second returns text
        const string expectedFinalResponse = "Based on the search results, I found 3 items for your query.";
        ConfigureWireMockForToolCallScenario(
            toolName: MockSearchTool.ToolName,
            toolArguments: """{"query":"test query"}""",
            finalResponseText: expectedFinalResponse);

        // Act — Send a message that should trigger the tool loop
        var sendResponse = await userClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Search for test query", model = "qwen-2.5-72b" });

        // Assert — Should get 201 with assistant response
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"SendMessage failed ({sendResponse.StatusCode}): {await sendResponse.Content.ReadAsStringAsync()}");

        var messageBody = await sendResponse.Content.ReadAsStringAsync();
        messageBody.ShouldNotBeNullOrWhiteSpace();

        using var messageDoc = JsonDocument.Parse(messageBody);
        var content = messageDoc.RootElement.GetProperty("content").GetString();
        content.ShouldBe(expectedFinalResponse);

        // Verify — WireMock received exactly 2 calls to chat/completions
        // (1st: tool_call response, 2nd: final text response after tool execution)
        var completionRequests = _factory.WireMock.LogEntries
            .Where(e => e.RequestMessage.Path == "/v1/chat/completions")
            .ToList();

        completionRequests.Count.ShouldBe(2,
            "Expected exactly 2 LLM calls: first returns tool_call, second returns text after tool execution.");

        // Verify — second request should contain a "tool" role message with the tool result
        var secondRequestBody = completionRequests[1].RequestMessage.Body;
        secondRequestBody.ShouldNotBeNull();
        secondRequestBody.ShouldContain(MockSearchTool.ExpectedResult);
    }

    // ──────────────────────────────────────────────────────────────
    // Test 2: No MCP server connected — graceful fallback
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoMcpServerConnected_SendMessage_ReturnsResponseWithoutToolCalls()
    {
        // Arrange — Do NOT register any MCP server. The MCP tool provider returns
        // zero MCP tools, but PluginToolProvider always returns CodeRunnerPlugin.
        // So the handler ALWAYS takes the tool-loop path (non-streaming JSON).
        // WireMock must return a JSON ChatCompletionResponse (not SSE).
        var userId = Guid.NewGuid().ToString();
        var userClient = CreateUserClient(userId);
        var conversationId = await CreateConversationAsync(userClient);

        // Configure WireMock for a simple JSON response (tool-loop path with no tool_calls returned)
        ConfigureWireMockForSimpleTextResponse("Hello from Mimir without tools");

        // Act
        var sendResponse = await userClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Hello", model = "qwen-2.5-72b" });

        // Assert — Should succeed with a normal text response
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"SendMessage failed: {await sendResponse.Content.ReadAsStringAsync()}");

        var body = await sendResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("content").GetString();
        content.ShouldNotBeNullOrWhiteSpace();
        content.ShouldContain("Hello from Mimir without tools");
    }

    // ──────────────────────────────────────────────────────────────
    // Test 3: Max iterations — tool loop stops after 5 iterations
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MaxIterations_ToolLoopStopsAfterFiveIterations_ReturnsFinalResponse()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var adminClient = CreateAdminClient(userId);
        var userClient = CreateUserClient(userId);

        // Register mock MCP server
        var serverId = await RegisterAndConnectMockMcpServerAsync(adminClient);

        var conversationId = await CreateConversationAsync(userClient);

        // Configure WireMock: always return tool_calls for 5 iterations,
        // then return text on the 6th (final) call
        const string expectedFallbackText = "I exceeded the maximum number of tool iterations.";
        ConfigureWireMockForInfiniteToolCalls(
            toolName: MockSearchTool.ToolName,
            finalFallbackText: expectedFallbackText);

        // Act
        var sendResponse = await userClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Keep searching forever", model = "qwen-2.5-72b" });

        // Assert — Should still return successfully after hitting max iterations
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"SendMessage failed after max iterations: {await sendResponse.Content.ReadAsStringAsync()}");

        var body = await sendResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("content").GetString();
        content.ShouldBe(expectedFallbackText);

        // Verify — WireMock should have received exactly 6 calls:
        // 5 tool-call iterations + 1 final no-tools call
        var completionRequests = _factory.WireMock.LogEntries
            .Where(e => e.RequestMessage.Path == "/v1/chat/completions")
            .ToList();
        completionRequests.Count.ShouldBe(6,
            "Expected 6 LLM calls: 5 tool-call iterations + 1 final fallback call");
    }

    // ──────────────────────────────────────────────────────────────
    // Test 4: Unreachable MCP server — graceful degradation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnreachableMcpServer_SendMessage_GracefullyDegradesToNoTools()
    {
        // Arrange — Create an MCP server config pointing to a non-existent URL.
        // The connection should fail, but the system should still work.
        // PluginToolProvider always returns CodeRunnerPlugin, so the handler takes
        // the tool-loop path. WireMock must return JSON ChatCompletionResponse.
        var userId = Guid.NewGuid().ToString();
        var adminClient = CreateAdminClient(userId);
        var userClient = CreateUserClient(userId);

        // Create MCP server config with unreachable URL
        var createResponse = await adminClient.PostAsJsonAsync("/api/mcp/servers", new
        {
            name = $"UnreachableMcpServer-{Guid.NewGuid():N}",
            transportType = 1, // Sse
            url = "http://127.0.0.1:1/sse", // Port 1 is almost certainly unreachable
            isEnabled = true,
            description = "E2E test unreachable MCP server",
        });

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"Failed to create unreachable MCP server: {await createResponse.Content.ReadAsStringAsync()}");

        // Wait for the config change listener polling interval to pick up changes
        await Task.Delay(TimeSpan.FromSeconds(12));

        // Wait for the connection attempt to fail
        await Task.Delay(2000);

        var conversationId = await CreateConversationAsync(userClient);

        // Configure WireMock for JSON response (tool-loop path — CodeRunner always present)
        ConfigureWireMockForSimpleTextResponse("Hello despite unreachable MCP server");

        // Act
        var sendResponse = await userClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Hello", model = "qwen-2.5-72b" });

        // Assert — Should still return a valid response despite the unreachable MCP server
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"SendMessage failed with unreachable MCP: {await sendResponse.Content.ReadAsStringAsync()}");

        var body = await sendResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("content").GetString();
        content.ShouldNotBeNullOrWhiteSpace();
    }

    // ──────────────────────────────────────────────────────────────
    // Test 5: MCP server admin API — CRUD operations
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task McpServerAdminApi_CreateAndListTools_RequiresAdminRole()
    {
        // Arrange — A non-admin user should get 403 when calling MCP server endpoints
        var regularClient = CreateUserClient();

        // Act — Try to list MCP servers without admin role
        var response = await regularClient.GetAsync("/api/mcp/servers");

        // Assert — Should be forbidden (403)
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task McpServerAdminApi_CreateServer_AdminCanCreateAndRetrieve()
    {
        // Arrange
        var adminClient = CreateAdminClient();
        var serverName = $"AdminApiTest-{Guid.NewGuid():N}";

        // Act — Create a server config
        var createResponse = await adminClient.PostAsJsonAsync("/api/mcp/servers", new
        {
            name = serverName,
            transportType = 1, // Sse
            url = _mockMcpServer!.SseUrl,
            isEnabled = false,
            description = "Admin API test",
        });

        // Assert
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var serverId = await createResponse.Content.ReadFromJsonAsync<Guid>();
        serverId.ShouldNotBe(Guid.Empty);

        // Verify — Retrieve the server
        var getResponse = await adminClient.GetAsync($"/api/mcp/servers/{serverId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await getResponse.Content.ReadAsStringAsync();
        body.ShouldContain(serverName);
    }

}
