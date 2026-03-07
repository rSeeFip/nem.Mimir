namespace nem.Mimir.PerformanceTests;

using System.Net;
using System.Text;
using NBomber.Contracts;
using NBomber.CSharp;

public static class PerformanceScenarios
{
    private static readonly TimeSpan StandardDuration = TimeSpan.FromSeconds(5);

    public const string ChatThroughputScenarioName = "ChatThroughput";
    public const string ChatThroughputStepName = "chat_request";

    public const string SandboxPoolScenarioName = "SandboxPool";
    public const string SandboxPoolStepName = "sandbox_execution";

    public const string McpToolDiscoveryScenarioName = "McpToolDiscovery";
    public const string McpToolDiscoveryStepName = "mcp_tool_discovery";

    public const string SemanticCacheScenarioName = "SemanticCache";
    public const string SemanticCacheStepName = "semantic_cache_lookup";

    public const string MemoryConsolidationScenarioName = "MemoryConsolidation";
    public const string MemoryConsolidationStepName = "memory_consolidation";

    public const string AgentOrchestrationScenarioName = "AgentOrchestration";
    public const string AgentOrchestrationStepName = "agent_orchestration";

    public static ScenarioProps ChatThroughput() =>
        CreateScenario(
            scenarioName: ChatThroughputScenarioName,
            stepName: ChatThroughputStepName,
            method: HttpMethod.Post,
            path: "/v1/chat/completions",
            requestBody: "{\"model\":\"gpt-4\",\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}",
            loadSimulation: Simulation.KeepConstant(copies: 50, during: StandardDuration),
            p95LatencyMsThreshold: 2000,
            baseDelayMs: 180,
            jitterMs: 45);

    public static ScenarioProps SandboxPool() =>
        CreateScenario(
            scenarioName: SandboxPoolScenarioName,
            stepName: SandboxPoolStepName,
            method: HttpMethod.Post,
            path: "/api/conversations/00000000-0000-0000-0000-000000000001/code-execution",
            requestBody: "{\"language\":\"python\",\"code\":\"print(42)\"}",
            loadSimulation: Simulation.KeepConstant(copies: 20, during: StandardDuration),
            p95LatencyMsThreshold: 500,
            baseDelayMs: 75,
            jitterMs: 15);

    public static ScenarioProps McpToolDiscovery() =>
        CreateScenario(
            scenarioName: McpToolDiscoveryScenarioName,
            stepName: McpToolDiscoveryStepName,
            method: HttpMethod.Get,
            path: "/api/models",
            requestBody: null,
            loadSimulation: Simulation.KeepConstant(copies: 100, during: StandardDuration),
            p95LatencyMsThreshold: 200,
            baseDelayMs: 25,
            jitterMs: 10);

    public static ScenarioProps SemanticCache() =>
        CreateScenario(
            scenarioName: SemanticCacheScenarioName,
            stepName: SemanticCacheStepName,
            method: HttpMethod.Get,
            path: "/api/models/gpt-4/status",
            requestBody: null,
            loadSimulation: Simulation.Inject(rate: 200, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(1)),
            p95LatencyMsThreshold: 50,
            baseDelayMs: 8,
            jitterMs: 6);

    public static ScenarioProps MemoryConsolidation() =>
        CreateScenario(
            scenarioName: MemoryConsolidationScenarioName,
            stepName: MemoryConsolidationStepName,
            method: HttpMethod.Post,
            path: "/v1/chat/completions",
            requestBody: "{\"model\":\"gpt-4\",\"messages\":[{\"role\":\"system\",\"content\":\"consolidate memory\"}]}",
            loadSimulation: Simulation.KeepConstant(copies: 10, during: StandardDuration),
            p95LatencyMsThreshold: 1000,
            baseDelayMs: 140,
            jitterMs: 40);

    public static ScenarioProps AgentOrchestration() =>
        CreateScenario(
            scenarioName: AgentOrchestrationScenarioName,
            stepName: AgentOrchestrationStepName,
            method: HttpMethod.Post,
            path: "/v1/chat/completions",
            requestBody: "{\"model\":\"gpt-4\",\"messages\":[{\"role\":\"user\",\"content\":\"orchestrate agents\"}]}",
            loadSimulation: Simulation.KeepConstant(copies: 30, during: StandardDuration),
            p95LatencyMsThreshold: 1200,
            baseDelayMs: 160,
            jitterMs: 35);

    private static ScenarioProps CreateScenario(
        string scenarioName,
        string stepName,
        HttpMethod method,
        string path,
        string? requestBody,
        LoadSimulation loadSimulation,
        int p95LatencyMsThreshold,
        int baseDelayMs,
        int jitterMs)
    {
        var handler = new MockRoutingHttpMessageHandler(
            new MockEndpoint(path, HttpStatusCode.OK, baseDelayMs, jitterMs, "{\"status\":\"ok\"}"));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://mimir.mock"),
            Timeout = TimeSpan.FromSeconds(30),
        };

        return Scenario
            .Create(scenarioName, async context =>
            {
                await Step.Run(stepName, context, async () =>
                {
                    using var request = new HttpRequestMessage(method, path);
                    if (!string.IsNullOrWhiteSpace(requestBody))
                    {
                        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    }

                    using var response = await httpClient.SendAsync(request);
                    var statusCode = ((int)response.StatusCode).ToString();

                    return response.IsSuccessStatusCode
                        ? Response.Ok(statusCode: statusCode)
                        : Response.Fail(statusCode: statusCode);
                });

                return Response.Ok();
            })
            .WithoutWarmUp()
            .WithLoadSimulations(loadSimulation)
            .WithThresholds(
                Threshold.Create(stepName, stepStats => stepStats.Ok.Latency.Percent95 < p95LatencyMsThreshold),
                Threshold.Create(stepName, stepStats => stepStats.Fail.Request.Percent == 0));
    }

    private sealed record MockEndpoint(
        string Path,
        HttpStatusCode StatusCode,
        int BaseDelayMs,
        int JitterMs,
        string ResponseBody);

    private sealed class MockRoutingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, MockEndpoint> _endpoints;

        public MockRoutingHttpMessageHandler(params MockEndpoint[] endpoints)
        {
            _endpoints = endpoints.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (!_endpoints.TryGetValue(path, out var endpoint))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"error\":\"not_found\"}", Encoding.UTF8, "application/json"),
                };
            }

            var delayMs = endpoint.BaseDelayMs + Random.Shared.Next(0, endpoint.JitterMs + 1);
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            return new HttpResponseMessage(endpoint.StatusCode)
            {
                Content = new StringContent(endpoint.ResponseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
