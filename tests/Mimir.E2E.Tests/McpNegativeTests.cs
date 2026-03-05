using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Mimir.E2E.Tests.Fixtures;
using Mimir.E2E.Tests.Helpers;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Mimir.E2E.Tests;

/// <summary>
/// Cross-service negative and edge-case E2E tests for the MCP tool pipeline.
/// Exercises security boundaries, error handling, concurrency, and limits.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class McpNegativeTests : IAsyncLifetime
{
    private readonly E2EWebApplicationFactory _factory;
    private MockMcpServer? _mockMcpServer;

    public McpNegativeTests(E2EWebApplicationFactory factory)
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

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an HttpClient with a JWT token that includes the "admin" role.
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
    /// Creates an HttpClient with a valid JWT token (user role only, no admin).
    /// </summary>
    private HttpClient CreateUserClient(string? userId = null)
    {
        return _factory.CreateAuthenticatedClient(userId);
    }

    /// <summary>
    /// Creates an unauthenticated HttpClient (no Authorization header).
    /// </summary>
    private HttpClient CreateUnauthenticatedClient()
    {
        return _factory.CreateClient();
    }

    /// <summary>
    /// Creates an MCP server config via the admin API and returns its ID.
    /// </summary>
    private async Task<Guid> CreateMcpServerConfigAsync(
        HttpClient adminClient,
        string? name = null,
        int transportType = 1, // Sse
        string? url = null,
        bool isEnabled = true,
        CancellationToken ct = default)
    {
        name ??= $"NegTest-{Guid.NewGuid():N}";
        url ??= _mockMcpServer!.SseUrl;

        var createResponse = await adminClient.PostAsJsonAsync("/api/mcp/servers", new
        {
            name,
            transportType,
            url,
            isEnabled,
            description = "Negative test MCP server",
        }, ct);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"Failed to create MCP server config: {await createResponse.Content.ReadAsStringAsync(ct)}");

        var serverId = await createResponse.Content.ReadFromJsonAsync<Guid>(ct);
        serverId.ShouldNotBe(Guid.Empty);
        return serverId;
    }

    /// <summary>
    /// Creates a conversation for the given user and returns its ID.
    /// </summary>
    private async Task<Guid> CreateConversationAsync(HttpClient userClient, CancellationToken ct = default)
    {
        var response = await userClient.PostAsJsonAsync("/api/conversations", new
        {
            title = $"MCP Negative Test {Guid.NewGuid():N}",
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
    /// Configures WireMock with scenario-based stateful behavior for tool calls.
    /// </summary>
    private void ConfigureWireMockForToolCallScenario(
        string toolName = "mock_search",
        string toolArguments = """{"query":"test query"}""",
        string finalResponseText = "Based on the search results, I found 3 items.")
    {
        _factory.WireMock.Reset();
        ConfigureWireMockBaseEndpoints();

        const string scenarioName = "NegTestToolFlow";

        // State 1 (initial → Started): Return tool call response
        _factory.WireMock
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .InScenario(scenarioName)
            .WhenStateIs("Started")
            .WillSetStateTo("ToolExecuted")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    id = "chatcmpl-neg-tool1",
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
                                        id = "call_neg_001",
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
                })));

        // State 2 (ToolExecuted): Return normal text response
        _factory.WireMock
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .InScenario(scenarioName)
            .WhenStateIs("ToolExecuted")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    id = "chatcmpl-neg-final",
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
                })));
    }

    /// <summary>
    /// Configures WireMock to always return tool_call responses (never a text response),
    /// used to test the max iterations safety limit.
    /// </summary>
    private void ConfigureWireMockForInfiniteToolCalls(
        string toolName = "mock_search",
        string toolArguments = """{"query":"infinite loop test"}""",
        string finalFallbackText = "I exceeded the maximum number of tool iterations.")
    {
        _factory.WireMock.Reset();
        ConfigureWireMockBaseEndpoints();

        const string scenarioName = "NegTestInfiniteTools";
        var states = new[] { "Started", "Iter1", "Iter2", "Iter3", "Iter4", "Iter5" };

        // First 5 states: always return tool_calls
        for (var i = 0; i < 5; i++)
        {
            var currentState = states[i];
            var nextState = states[i + 1];

            _factory.WireMock
                .Given(Request.Create()
                    .WithPath("/v1/chat/completions")
                    .UsingPost())
                .InScenario(scenarioName)
                .WhenStateIs(currentState)
                .WillSetStateTo(nextState)
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(JsonSerializer.Serialize(new
                    {
                        id = $"chatcmpl-neg-iter{i + 1}",
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
                                            id = $"call_neg_iter_{i + 1}",
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
                    })));
        }

        // State 6 (Iter5): The final call WITHOUT tools — return text response
        _factory.WireMock
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .InScenario(scenarioName)
            .WhenStateIs("Iter5")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    id = "chatcmpl-neg-final-fallback",
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
                })));
    }

    /// <summary>
    /// Configures WireMock for a streaming SSE response (no tool calls path).
    /// </summary>
    private void ConfigureWireMockForStreamingResponse(string responseText = "Hello from Mimir")
    {
        _factory.WireMock.Reset();
        ConfigureWireMockBaseEndpoints();

        _factory.WireMock
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/event-stream")
                .WithHeader("Cache-Control", "no-cache")
                .WithBody(BuildStreamingResponse(responseText)));
    }

    /// <summary>
    /// Configures WireMock for a simple non-streaming text response (no tool calls).
    /// </summary>
    private void ConfigureWireMockForSimpleTextResponse(string responseText = "Hello from Mimir")
    {
        _factory.WireMock.Reset();
        ConfigureWireMockBaseEndpoints();

        _factory.WireMock
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    id = "chatcmpl-neg-simple",
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

    /// <summary>
    /// Re-adds the health and models endpoints to WireMock after a reset.
    /// </summary>
    private void ConfigureWireMockBaseEndpoints()
    {
        _factory.WireMock
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"status":"healthy"}"""));

        _factory.WireMock
            .Given(Request.Create().WithPath("/v1/models").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"object":"list","data":[{"id":"qwen-2.5-72b","object":"model","created":1234567890,"owned_by":"litellm"}]}"""));
    }

    /// <summary>
    /// Builds an SSE streaming response body for WireMock.
    /// </summary>
    private static string BuildStreamingResponse(string fullText)
    {
        var sb = new StringBuilder();
        var words = fullText.Split(' ');

        for (var i = 0; i < words.Length; i++)
        {
            var chunk = i == 0 ? words[i] : $" {words[i]}";

            if (i == 0)
            {
                sb.AppendLine($"data: {{\"id\":\"chatcmpl-neg-stream\",\"object\":\"chat.completion.chunk\",\"created\":1234567890,\"model\":\"qwen-2.5-72b\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"content\":\"{EscapeJsonString(chunk)}\"}},\"finish_reason\":null}}]}}");
            }
            else
            {
                sb.AppendLine($"data: {{\"id\":\"chatcmpl-neg-stream\",\"object\":\"chat.completion.chunk\",\"created\":1234567890,\"model\":\"qwen-2.5-72b\",\"choices\":[{{\"index\":0,\"delta\":{{\"content\":\"{EscapeJsonString(chunk)}\"}},\"finish_reason\":null}}]}}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("data: {\"id\":\"chatcmpl-neg-stream\",\"object\":\"chat.completion.chunk\",\"created\":1234567890,\"model\":\"qwen-2.5-72b\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}");
        sb.AppendLine();
        sb.AppendLine("data: [DONE]");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string EscapeJsonString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    // ══════════════════════════════════════════════════════════════
    // AUTH FAILURES
    // ══════════════════════════════════════════════════════════════

    // ──────────────────────────────────────────────────────────────
    // Test 1: Unauthenticated request to MCP admin endpoints → 401
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET", "/api/mcp/servers")]
    [InlineData("POST", "/api/mcp/servers")]
    [InlineData("GET", "/api/mcp/servers/audit")]
    public async Task UnauthenticatedRequest_ToMcpAdminEndpoints_ShouldReturn401(string method, string endpoint)
    {
        // Arrange — no JWT token at all
        var client = CreateUnauthenticatedClient();

        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        if (method == "POST")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request);

        // Assert — must be 401 Unauthorized (not 403)
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,
            $"Expected 401 for unauthenticated {method} {endpoint}, got {(int)response.StatusCode}");
    }

    // ──────────────────────────────────────────────────────────────
    // Test 2: Non-admin user accessing MCP admin endpoints → 403
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET", "/api/mcp/servers")]
    [InlineData("POST", "/api/mcp/servers")]
    [InlineData("GET", "/api/mcp/servers/audit")]
    public async Task NonAdminUser_ShouldReturn403_ForMcpAdminEndpoints(string method, string endpoint)
    {
        // Arrange — user-role JWT (no admin)
        var client = CreateUserClient();

        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        if (method == "POST")
        {
            request.Content = new StringContent(
                """{"name":"test","transportType":1,"url":"http://localhost:5000/sse","isEnabled":false}""",
                Encoding.UTF8,
                "application/json");
        }

        var response = await client.SendAsync(request);

        // Assert — must be 403 Forbidden
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden,
            $"Expected 403 for non-admin {method} {endpoint}, got {(int)response.StatusCode}");
    }

    // ══════════════════════════════════════════════════════════════
    // MCP SERVER FAILURES
    // ══════════════════════════════════════════════════════════════

    // ──────────────────────────────────────────────────────────────
    // Test 3: Unreachable MCP server → graceful degradation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task McpServerUnreachable_ShouldReturnGracefulError()
    {
        // Arrange — Create MCP server config pointing to unreachable URL.
        // The connection should fail, but the system should still work (no tools available).
        var userId = Guid.NewGuid().ToString();
        var adminClient = CreateAdminClient(userId);
        var userClient = CreateUserClient(userId);

        var serverId = await CreateMcpServerConfigAsync(
            adminClient,
            name: $"Unreachable-{Guid.NewGuid():N}",
            url: "http://127.0.0.1:1/sse"); // Port 1 is unreachable

        // Wait for config change listener to pick up changes + connection attempt to fail
        await Task.Delay(TimeSpan.FromSeconds(14));

        var conversationId = await CreateConversationAsync(userClient);

        // Configure WireMock for streaming (no-tools path since MCP server is unreachable)
        ConfigureWireMockForStreamingResponse("Hello despite unreachable MCP server");

        // Act
        var sendResponse = await userClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Hello", model = "qwen-2.5-72b" });

        // Assert — Should degrade gracefully: return a response without tool results
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"SendMessage failed with unreachable MCP: {await sendResponse.Content.ReadAsStringAsync()}");

        var body = await sendResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("content").GetString();
        content.ShouldNotBeNullOrWhiteSpace("Response content should not be empty even with unreachable MCP");
    }

    // ──────────────────────────────────────────────────────────────
    // Test 4: MCP server timeout → system doesn't block forever
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task McpServerTimeout_ShouldNotBlockForever()
    {
        // Arrange — Create MCP server with URL that will never respond.
        // We configure it but it won't connect. The test verifies the system
        // continues to function (doesn't hang) within a reasonable time.
        var userId = Guid.NewGuid().ToString();
        var adminClient = CreateAdminClient(userId);
        var userClient = CreateUserClient(userId);

        // Use a non-routable IP to simulate a hanging connection
        var serverId = await CreateMcpServerConfigAsync(
            adminClient,
            name: $"Timeout-{Guid.NewGuid():N}",
            url: "http://192.0.2.1:9999/sse"); // TEST-NET-1 (RFC 5737), not routable

        // Wait for config change listener polling interval
        await Task.Delay(TimeSpan.FromSeconds(14));

        var conversationId = await CreateConversationAsync(userClient);

        ConfigureWireMockForStreamingResponse("Response after timeout MCP server");

        // Act — Use a CancellationToken with timeout to prove the system doesn't block
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var sendResponse = await userClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Hello with timeout MCP", model = "qwen-2.5-72b" },
            cts.Token);

        // Assert — Should return within the timeout, not hang forever
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"SendMessage blocked or failed: {await sendResponse.Content.ReadAsStringAsync()}");

        var body = await sendResponse.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace();
    }

    // ══════════════════════════════════════════════════════════════
    // CONFIG VALIDATION
    // ══════════════════════════════════════════════════════════════

    // ──────────────────────────────────────────────────────────────
    // Test 5: Missing required fields → error response
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateMcpConfig_WithMissingRequiredFields_ShouldReturnError()
    {
        // Arrange
        var adminClient = CreateAdminClient();

        // Act — POST with empty/null name (required field)
        var response = await adminClient.PostAsync(
            "/api/mcp/servers",
            new StringContent(
                """{"name":null,"transportType":1,"url":"http://localhost:5000/sse"}""",
                Encoding.UTF8,
                "application/json"));

        // Assert — Should return 4xx (either 400 from validation or 500 from DB constraint)
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Creating config with null name should fail, got {statusCode}");
    }

    // ──────────────────────────────────────────────────────────────
    // Test 6: Invalid transport type → error response
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateMcpConfig_WithInvalidTransportType_ShouldReturnError()
    {
        // Arrange
        var adminClient = CreateAdminClient();

        // Act — POST with invalid transport type (99 is not a valid enum value)
        var response = await adminClient.PostAsync(
            "/api/mcp/servers",
            new StringContent(
                """{"name":"InvalidTransport","transportType":99,"url":"http://localhost:5000/sse"}""",
                Encoding.UTF8,
                "application/json"));

        // Assert — Should return 4xx (bad request from JSON deserialization or validation)
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Creating config with invalid transport type should fail, got {statusCode}");
    }

    // ──────────────────────────────────────────────────────────────
    // Test 7: Malformed JSON body → error response
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateMcpConfig_WithMalformedJson_ShouldReturnError()
    {
        // Arrange
        var adminClient = CreateAdminClient();

        // Act — POST with malformed JSON
        var response = await adminClient.PostAsync(
            "/api/mcp/servers",
            new StringContent(
                "{this is not json!!}",
                Encoding.UTF8,
                "application/json"));

        // Assert — Should return 400 (bad request)
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Malformed JSON should fail, got {statusCode}");
    }

    // ══════════════════════════════════════════════════════════════
    // TOOL SAFETY
    // ══════════════════════════════════════════════════════════════

    // ──────────────────────────────────────────────────────────────
    // Test 8: No whitelisted tools → LLM sees no tools, no tool calls
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoWhitelistedTools_ShouldNotTriggerToolCalls()
    {
        // Arrange — Register MCP server, connect it, but DON'T whitelist any tools.
        // The ToolWhitelistFilter should filter out all tools, so the LLM gets zero
        // tool definitions and takes the streaming path (no tool loop).
        var userId = Guid.NewGuid().ToString();
        var adminClient = CreateAdminClient(userId);
        var userClient = CreateUserClient(userId);

        // Create the server config but do NOT set any whitelist entries
        var serverId = await CreateMcpServerConfigAsync(adminClient);

        // Wait for config change listener + MCP connection handshake
        await Task.Delay(TimeSpan.FromSeconds(14));

        var conversationId = await CreateConversationAsync(userClient);

        // WireMock: streaming response (no-tools path)
        ConfigureWireMockForStreamingResponse("Response without any tools available");

        // Act
        var sendResponse = await userClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Search for something", model = "qwen-2.5-72b" });

        // Assert — Should succeed via streaming path, no tool calls
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"SendMessage failed: {await sendResponse.Content.ReadAsStringAsync()}");

        var body = await sendResponse.Content.ReadAsStringAsync();
        using var msgDoc = JsonDocument.Parse(body);
        var content = msgDoc.RootElement.GetProperty("content").GetString();
        content.ShouldNotBeNull();
        content.ShouldContain("Response without any tools available");

        // Verify — WireMock should have received exactly 1 call (streaming, no tool loop)
        var completionRequests = _factory.WireMock.LogEntries
            .Where(e => e.RequestMessage.Path == "/v1/chat/completions")
            .ToList();
        completionRequests.Count.ShouldBe(1,
            "Expected exactly 1 LLM call (streaming path, no tools)");

        // The request body should NOT contain any "tools" definitions
        var requestBody = completionRequests[0].RequestMessage.Body;
        if (requestBody is not null)
        {
            using var reqDoc = JsonDocument.Parse(requestBody);
            var hasTools = reqDoc.RootElement.TryGetProperty("tools", out var toolsElement);
            if (hasTools)
            {
                // If tools property exists, it should be null or empty
                (toolsElement.ValueKind == JsonValueKind.Null ||
                 (toolsElement.ValueKind == JsonValueKind.Array && toolsElement.GetArrayLength() == 0))
                    .ShouldBeTrue("Tools array should be null or empty when nothing is whitelisted");
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Test 9: Oversized tool arguments → rejection by whitelist filter
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task OversizedToolArguments_ShouldBeHandledGracefully()
    {
        // Arrange — Register + connect + whitelist mock_search tool
        var userId = Guid.NewGuid().ToString();
        var adminClient = CreateAdminClient(userId);
        var userClient = CreateUserClient(userId);

        var serverId = await CreateMcpServerConfigAsync(adminClient);
        await Task.Delay(TimeSpan.FromSeconds(14));

        // Whitelist the mock_search tool
        var whitelistResponse = await adminClient.PutAsJsonAsync(
            $"/api/mcp/servers/{serverId}/whitelist",
            new
            {
                entries = new[]
                {
                    new { toolName = MockSearchTool.ToolName, isEnabled = true },
                },
            });
        whitelistResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var conversationId = await CreateConversationAsync(userClient);

        // Configure WireMock to return a tool call with oversized arguments (>10KB)
        var oversizedArgs = new string('x', 11_000); // > 10KB limit
        var oversizedJson = JsonSerializer.Serialize(new { query = oversizedArgs });

        ConfigureWireMockForToolCallScenario(
            toolName: MockSearchTool.ToolName,
            toolArguments: oversizedJson,
            finalResponseText: "Recovered after oversized arguments were rejected.");

        // Act — Send a message that triggers the tool loop with oversized args
        var sendResponse = await userClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Search with large input", model = "qwen-2.5-72b" });

        // Assert — The system should handle this gracefully (not crash).
        // The whitelist filter rejects the arguments, but the tool loop continues
        // with a failure result and eventually the LLM produces a final answer.
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"SendMessage failed with oversized args: {await sendResponse.Content.ReadAsStringAsync()}");

        var body = await sendResponse.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace();
    }

    // ──────────────────────────────────────────────────────────────
    // Test 10: Path traversal in tool args → rejection by safety filter
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PathTraversalInToolArgs_ShouldBeHandledGracefully()
    {
        // Arrange — Register + connect + whitelist tool
        var userId = Guid.NewGuid().ToString();
        var adminClient = CreateAdminClient(userId);
        var userClient = CreateUserClient(userId);

        var serverId = await CreateMcpServerConfigAsync(adminClient);
        await Task.Delay(TimeSpan.FromSeconds(14));

        var whitelistResponse = await adminClient.PutAsJsonAsync(
            $"/api/mcp/servers/{serverId}/whitelist",
            new
            {
                entries = new[]
                {
                    new { toolName = MockSearchTool.ToolName, isEnabled = true },
                },
            });
        whitelistResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var conversationId = await CreateConversationAsync(userClient);

        // Configure WireMock to return a tool call with path traversal patterns
        var pathTraversalArgs = """{"query":"../../etc/passwd"}""";

        ConfigureWireMockForToolCallScenario(
            toolName: MockSearchTool.ToolName,
            toolArguments: pathTraversalArgs,
            finalResponseText: "Recovered after path traversal was blocked.");

        // Act
        var sendResponse = await userClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Read sensitive file", model = "qwen-2.5-72b" });

        // Assert — The system should handle this gracefully.
        // Path traversal is caught by ValidateArguments, tool returns failure,
        // but the pipeline continues to a final LLM response.
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"SendMessage failed with path traversal: {await sendResponse.Content.ReadAsStringAsync()}");

        var body = await sendResponse.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace();
    }

    // ──────────────────────────────────────────────────────────────
    // Test 11: SQL injection in tool args → rejection by safety filter
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SqlInjectionInToolArgs_ShouldBeHandledGracefully()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var adminClient = CreateAdminClient(userId);
        var userClient = CreateUserClient(userId);

        var serverId = await CreateMcpServerConfigAsync(adminClient);
        await Task.Delay(TimeSpan.FromSeconds(14));

        var whitelistResponse = await adminClient.PutAsJsonAsync(
            $"/api/mcp/servers/{serverId}/whitelist",
            new
            {
                entries = new[]
                {
                    new { toolName = MockSearchTool.ToolName, isEnabled = true },
                },
            });
        whitelistResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var conversationId = await CreateConversationAsync(userClient);

        // Configure WireMock to return a tool call with SQL injection patterns
        var sqlInjectionArgs = """{"query":"'; DROP TABLE users; --"}""";

        ConfigureWireMockForToolCallScenario(
            toolName: MockSearchTool.ToolName,
            toolArguments: sqlInjectionArgs,
            finalResponseText: "Recovered after SQL injection was blocked.");

        // Act
        var sendResponse = await userClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Query database", model = "qwen-2.5-72b" });

        // Assert — Pipeline continues despite blocked tool execution
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"SendMessage failed with SQL injection args: {await sendResponse.Content.ReadAsStringAsync()}");

        var body = await sendResponse.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace();
    }

    // ══════════════════════════════════════════════════════════════
    // CONCURRENCY / LIMITS
    // ══════════════════════════════════════════════════════════════

    // ──────────────────────────────────────────────────────────────
    // Test 12: Max tool iterations stops at 5
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MaxToolIterations_ShouldStopAtFive()
    {
        // Arrange — Set up a scenario where the LLM always returns tool calls.
        // The handler should stop after 5 tool iterations and make a final call without tools.
        var userId = Guid.NewGuid().ToString();
        var adminClient = CreateAdminClient(userId);
        var userClient = CreateUserClient(userId);

        var serverId = await CreateMcpServerConfigAsync(adminClient);
        await Task.Delay(TimeSpan.FromSeconds(14));

        var whitelistResponse = await adminClient.PutAsJsonAsync(
            $"/api/mcp/servers/{serverId}/whitelist",
            new
            {
                entries = new[]
                {
                    new { toolName = MockSearchTool.ToolName, isEnabled = true },
                },
            });
        whitelistResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var conversationId = await CreateConversationAsync(userClient);

        const string expectedFallbackText = "I exceeded the maximum number of tool iterations.";
        ConfigureWireMockForInfiniteToolCalls(
            toolName: MockSearchTool.ToolName,
            finalFallbackText: expectedFallbackText);

        // Act
        var sendResponse = await userClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Keep searching forever", model = "qwen-2.5-72b" });

        // Assert — Should return successfully after hitting max iterations
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

        // Additional verification: the 6th (final) call should NOT contain "tools" in the request
        var finalRequestBody = completionRequests[5].RequestMessage.Body;
        if (finalRequestBody is not null)
        {
            using var reqDoc = JsonDocument.Parse(finalRequestBody);
            var hasTools = reqDoc.RootElement.TryGetProperty("tools", out var toolsElement);
            if (hasTools)
            {
                (toolsElement.ValueKind == JsonValueKind.Null ||
                 (toolsElement.ValueKind == JsonValueKind.Array && toolsElement.GetArrayLength() == 0))
                    .ShouldBeTrue("Final fallback call should not include tools");
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Test 13: Simultaneous config updates → no corruption
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SimultaneousConfigUpdates_ShouldHandleGracefully()
    {
        // Arrange — Create an MCP server config, then fire concurrent updates
        var adminClient = CreateAdminClient();
        var serverId = await CreateMcpServerConfigAsync(adminClient, isEnabled: false);

        // Act — Fire 5 concurrent toggle requests (enable/disable rapidly)
        var tasks = Enumerable.Range(0, 5).Select(i =>
            adminClient.PostAsJsonAsync(
                $"/api/mcp/servers/{serverId}/toggle",
                new { isEnabled = i % 2 == 0 }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert — All requests should complete without server errors (no 5xx)
        foreach (var response in responses)
        {
            var statusCode = (int)response.StatusCode;
            (statusCode < 500).ShouldBeTrue(
                $"Concurrent toggle returned server error: {statusCode}");
        }

        // Verify — The config should still be retrievable (not corrupted)
        var getResponse = await adminClient.GetAsync($"/api/mcp/servers/{serverId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
            "Config should still be retrievable after concurrent updates");
    }

    // ──────────────────────────────────────────────────────────────
    // Test 14: Concurrent config creates → all succeed independently
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentConfigCreates_ShouldAllSucceed()
    {
        // Arrange
        var adminClient = CreateAdminClient();

        // Act — Fire 5 concurrent server config creates
        var tasks = Enumerable.Range(0, 5).Select(i =>
            adminClient.PostAsJsonAsync("/api/mcp/servers", new
            {
                name = $"ConcurrentCreate-{i}-{Guid.NewGuid():N}",
                transportType = 1, // Sse
                url = $"http://localhost:{10000 + i}/sse",
                isEnabled = false,
                description = $"Concurrent create test {i}",
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert — All should return 201 Created
        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Created,
                $"Concurrent create failed: {await response.Content.ReadAsStringAsync()}");

            var serverId = await response.Content.ReadFromJsonAsync<Guid>();
            serverId.ShouldNotBe(Guid.Empty);
        }

        // Verify — All 5 should be in the list
        var listResponse = await adminClient.GetAsync("/api/mcp/servers");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        listBody.ShouldContain("ConcurrentCreate-0");
        listBody.ShouldContain("ConcurrentCreate-4");
    }

    // ══════════════════════════════════════════════════════════════
    // DELETION
    // ══════════════════════════════════════════════════════════════

    // ──────────────────────────────────────────────────────────────
    // Test 15: Delete MCP config → removed from database
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteMcpConfig_ShouldRemoveFromDatabase()
    {
        // Arrange — Create an MCP server config
        var adminClient = CreateAdminClient();
        var serverId = await CreateMcpServerConfigAsync(adminClient, isEnabled: false);

        // Verify it exists
        var getResponse = await adminClient.GetAsync($"/api/mcp/servers/{serverId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act — Delete it
        var deleteResponse = await adminClient.DeleteAsync($"/api/mcp/servers/{serverId}");

        // Assert — Delete should succeed
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent,
            $"Delete failed: {await deleteResponse.Content.ReadAsStringAsync()}");

        // Verify — Config should no longer be retrievable
        var getAfterDelete = await adminClient.GetAsync($"/api/mcp/servers/{serverId}");
        var statusCode = (int)getAfterDelete.StatusCode;
        (statusCode == 404 || statusCode >= 400).ShouldBeTrue(
            $"Expected 404 after deletion, got {statusCode}");
    }

    // ──────────────────────────────────────────────────────────────
    // Test 16: Delete nonexistent config → error
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteMcpConfig_Nonexistent_ShouldReturnError()
    {
        // Arrange
        var adminClient = CreateAdminClient();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await adminClient.DeleteAsync($"/api/mcp/servers/{nonExistentId}");

        // Assert — Should return 404 or other error
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Deleting nonexistent config should fail, got {statusCode}");
    }

    // ══════════════════════════════════════════════════════════════
    // WHITELIST EDGE CASES
    // ══════════════════════════════════════════════════════════════

    // ──────────────────────────────────────────────────────────────
    // Test 17: Whitelist for nonexistent server → error
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateWhitelist_ForNonexistentServer_ShouldReturnError()
    {
        // Arrange
        var adminClient = CreateAdminClient();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await adminClient.PutAsJsonAsync(
            $"/api/mcp/servers/{nonExistentId}/whitelist",
            new
            {
                entries = new[]
                {
                    new { toolName = "some_tool", isEnabled = true },
                },
            });

        // Assert — Should return 404
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Updating whitelist for nonexistent server should fail, got {statusCode}");
    }

    // ──────────────────────────────────────────────────────────────
    // Test 18: Toggle nonexistent server → error
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleServer_Nonexistent_ShouldReturnError()
    {
        // Arrange
        var adminClient = CreateAdminClient();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await adminClient.PostAsJsonAsync(
            $"/api/mcp/servers/{nonExistentId}/toggle",
            new { isEnabled = true });

        // Assert — Should return 404
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Toggling nonexistent server should fail, got {statusCode}");
    }
}
