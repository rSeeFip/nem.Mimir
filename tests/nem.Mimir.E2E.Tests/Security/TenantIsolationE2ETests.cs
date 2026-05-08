using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using nem.Mimir.Application.Billing;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.E2E.Tests.Helpers;
using nem.Mimir.Infrastructure.Billing;
using nem.Mimir.Infrastructure.Caching;
using Shouldly;

namespace nem.Mimir.E2E.Tests.Security;

/// <summary>
/// E2E tests for tenant data isolation enforcement across tenant-scoped resources.
/// Verifies JWT tenant claims drive conversation, billing, model cache, and MCP server isolation.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class TenantIsolationE2ETests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    private readonly E2EWebApplicationFactory _factory;

    public TenantIsolationE2ETests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Conversations ───────────────────────────────────────────────────────

    [Fact]
    public async Task TenantIsolation_TenantACannotReadTenantBConversation_ReturnsNotFoundExceptionBoundary()
    {
        // Arrange — same user identity, different tenant claims isolates access by tenant
        var userId = Guid.NewGuid().ToString();
        var tenantAClient = _factory.CreateAuthenticatedClient(userId, tenantId: TenantA);
        var tenantBClient = _factory.CreateAuthenticatedClient(userId, tenantId: TenantB);

        var conversationId = await CreateConversationAsync(tenantBClient, "Tenant B secret conversation");

        // Act — tenant A attempts to read tenant B's conversation by id
        var response = await tenantAClient.GetAsync($"/api/conversations/{conversationId}");

        // Assert — E2E factory surfaces app exceptions as 500, but the hidden resource remains not-found across tenants
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("NotFoundException");
    }

    // ── Billing ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task TenantIsolation_TenantACannotReadTenantBBilling_ReturnsOwnSummaryOnly()
    {
        // Arrange — seed usage for both tenants in the same period
        var from = DateTimeOffset.UtcNow.AddHours(-1);
        var to = DateTimeOffset.UtcNow.AddHours(1);

        await SeedBillingEventAsync(TenantA, "shared-user", "qwen-2.5-72b", 100, 40, 0.12m, from.AddMinutes(5));
        await SeedBillingEventAsync(TenantB, "shared-user", "gpt-4o", 900, 300, 4.56m, from.AddMinutes(10));

        var tenantAClient = _factory.CreateAuthenticatedClient(Guid.NewGuid().ToString(), tenantId: TenantA);

        // Act — tenant A requests billing summary for a range containing both tenants' events
        var response = await tenantAClient.GetAsync($"/api/billing/usage?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        // Assert — only tenant A usage is returned
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<TenantUsageSummary>();
        summary.ShouldNotBeNull();
        summary.TenantId.ShouldBe(TenantA);
        summary.RequestCount.ShouldBe(1);
        summary.TotalInputTokens.ShouldBe(100);
        summary.TotalOutputTokens.ShouldBe(40);
        summary.TotalCost.ShouldBe(0.12m);
        summary.ModelBreakdown.Keys.ShouldBe(["qwen-2.5-72b"]);
        summary.ModelBreakdown.ContainsKey("gpt-4o").ShouldBeFalse();
    }

    // ── Models / cache isolation ────────────────────────────────────────────

    [Fact]
    public async Task TenantIsolation_ModelsCacheDoesNotLeakAcrossTenants_UsesDistinctTenantKeys()
    {
        // Arrange — same endpoint payload for both tenants, but cache scope must stay tenant-bound
        _factory.WireMock.Reset();
        ConfigureModelsStub();

        var tenantAClient = _factory.CreateAuthenticatedClient(Guid.NewGuid().ToString(), tenantId: TenantA);
        var tenantBClient = _factory.CreateAuthenticatedClient(Guid.NewGuid().ToString(), tenantId: TenantB);

        // Act — each tenant requests models once
        var tenantAResponse = await tenantAClient.GetAsync("/api/models");
        var tenantBResponse = await tenantBClient.GetAsync("/api/models");

        // Assert — both succeed, and LiteLLM is hit twice because cache keys differ per tenant
        tenantAResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        tenantBResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var requestsToLiteLlm = _factory.WireMock.LogEntries
            .Count(entry => entry.RequestMessage.Path == "/v1/models");
        requestsToLiteLlm.ShouldBe(2);

        TenantAwareCacheKey.Build(TenantA, TenantAwareCacheKey.Keys.Models)
            .ShouldNotBe(TenantAwareCacheKey.Build(TenantB, TenantAwareCacheKey.Keys.Models));
    }

    [Fact]
    public void TenantIsolation_TenantAwareCacheKeys_AreDifferentAcrossTenants()
    {
        // Arrange / Act
        var tenantAKey = TenantAwareCacheKey.Build(TenantA, "shared-key");
        var tenantBKey = TenantAwareCacheKey.Build(TenantB, "shared-key");

        // Assert — cross-tenant cache poisoning is impossible because tenant prefixes differ
        tenantAKey.ShouldBe("tenant:tenant-a:shared-key");
        tenantBKey.ShouldBe("tenant:tenant-b:shared-key");
        tenantAKey.ShouldNotBe(tenantBKey);
    }

    // ── MCP servers ─────────────────────────────────────────────────────────

    [Fact]
    public async Task TenantIsolation_TenantACannotReadTenantBMcpServer_ReturnsNotFoundExceptionBoundary()
    {
        // Arrange — admin role in both tenants, same user, tenant B creates the server config
        var userId = Guid.NewGuid().ToString();
        var tenantAAdmin = CreateAdminClient(TenantA, userId);
        var tenantBAdmin = CreateAdminClient(TenantB, userId);

        var mcpServerId = await CreateMcpServerAsync(tenantBAdmin, "Tenant B MCP server");

        // Act — tenant A attempts to read tenant B's server config
        var response = await tenantAAdmin.GetAsync($"/api/mcp/servers/{mcpServerId}");

        // Assert — E2E factory surfaces app exceptions as 500, but the hidden resource remains not-found across tenants
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("NotFoundException");
    }

    // ── Missing tenant claim enforcement ────────────────────────────────────

    [Fact]
    public async Task TenantIsolation_RequestWithoutTenantClaim_Returns403()
    {
        // Arrange — authenticated token without tenant_id claim
        var client = CreateClientWithoutTenantClaim();

        // Act
        var response = await client.GetAsync("/api/conversations");

        // Assert — middleware blocks authenticated requests missing tenant context
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private HttpClient CreateAdminClient(string tenantId, string? userId = null)
    {
        userId ??= Guid.NewGuid().ToString();
        return _factory.CreateAuthenticatedClient(userId, tenantId: tenantId, roles: ["admin"]);
    }

    private HttpClient CreateClientWithoutTenantClaim()
    {
        var client = _factory.CreateClient();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtTokenHelper.TestSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var userId = Guid.NewGuid().ToString();

        var token = new JwtSecurityToken(
            issuer: JwtTokenHelper.TestIssuer,
            audience: JwtTokenHelper.TestAudience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(JwtRegisteredClaimNames.Email, $"missing-tenant-{userId[..8]}@test.local"),
                new Claim(ClaimTypes.Role, "user"),
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        return client;
    }

    private async Task<Guid> CreateConversationAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/conversations", new
        {
            title,
            model = "qwen-2.5-72b",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"Failed to create conversation: {await response.Content.ReadAsStringAsync()}");

        var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>();
        conversation.ShouldNotBeNull();
        return conversation.Id;
    }

    private async Task<Guid> CreateMcpServerAsync(HttpClient adminClient, string name)
    {
        var response = await adminClient.PostAsJsonAsync("/api/mcp/servers", new
        {
            name,
            transportType = 1,
            url = "http://localhost:5999/sse",
            isEnabled = false,
            description = "Tenant isolation test MCP server",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"Failed to create MCP server: {await response.Content.ReadAsStringAsync()}");

        var serverId = await response.Content.ReadFromJsonAsync<Guid>();
        serverId.ShouldNotBe(Guid.Empty);
        return serverId;
    }

    private async Task SeedBillingEventAsync(
        string tenantId,
        string userId,
        string model,
        int promptTokens,
        int completionTokens,
        decimal costUsd,
        DateTimeOffset occurredAt)
    {
        using var scope = _factory.Services.CreateScope();
        var documentStore = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        await using var session = documentStore.LightweightSession();

        var billingEvent = new PersistedCostEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
            CostUsd = costUsd,
            Channel = "web",
            OccurredAt = occurredAt,
            IdempotencyKey = PersistedCostEvent.ComputeIdempotencyKey(tenantId, userId, model, occurredAt),
        };

        session.Store(billingEvent);
        await session.SaveChangesAsync();
    }

    private void ConfigureModelsStub()
    {
        _factory.WireMock
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/v1/models")
                .UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                        "object": "list",
                        "data": [
                            {
                                "id": "qwen-2.5-72b",
                                "object": "model",
                                "created": 1234567890,
                                "owned_by": "litellm"
                            }
                        ]
                    }
                    """));

        _factory.WireMock
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/health")
                .UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"status\":\"healthy\"}"));
    }
}
