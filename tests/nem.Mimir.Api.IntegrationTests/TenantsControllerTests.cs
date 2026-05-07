using System.Net;
using System.Net.Http.Json;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Domain.Tenants;
using Shouldly;

namespace nem.Mimir.Api.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class TenantsControllerTests(MimirWebApplicationFactory factory)
{
    [Fact]
    public async Task CreateTenant_WithPlatformAdmin_CreatesTenantWithDefaultRateLimit()
    {
        var client = factory.CreateAuthenticatedClient(roles: ["platform-admin"]);

        var response = await client.PostAsJsonAsync("/api/tenants", new
        {
            Name = "Acme Corp",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();
        tenant.ShouldNotBeNull();
        tenant.DefaultRateLimit.ShouldBe(100);
        tenant.Status.ShouldBe(TenantStatus.Active);
        tenant.Slug.ShouldBe("acme-corp");

        var listResponse = await client.GetAsync("/api/tenants");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = await listResponse.Content.ReadFromJsonAsync<List<TenantResponse>>();
        tenants.ShouldNotBeNull();
        tenants.ShouldContain(x => x.Id == tenant.Id);
    }

    [Fact]
    public async Task DeleteTenant_WithPlatformAdmin_SoftDeletesAndPreservesDataForThirtyDays()
    {
        var client = factory.CreateAuthenticatedClient(roles: ["platform-admin"]);

        var createResponse = await client.PostAsJsonAsync("/api/tenants", new
        {
            Name = "Contoso",
            DefaultRateLimit = 250,
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();
        tenant.ShouldNotBeNull();

        var beforeDelete = DateTimeOffset.UtcNow;
        var deleteResponse = await client.DeleteAsync($"/api/tenants/{tenant.Id}");
        var afterDelete = DateTimeOffset.UtcNow;

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.QuerySession();
        var persisted = await session.Query<Tenant>().FirstOrDefaultAsync(x => x.Id == tenant.Id);

        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(TenantStatus.Offboarded);
        persisted.DefaultRateLimit.ShouldBe(250);
        persisted.OffboardedAt.ShouldNotBeNull();
        persisted.DataRetentionUntil.ShouldNotBeNull();
        persisted.OffboardedAt.Value.ShouldBeInRange(beforeDelete.AddMinutes(-1), afterDelete.AddMinutes(1));
        persisted.DataRetentionUntil.Value.ShouldBeInRange(beforeDelete.AddDays(30).AddMinutes(-1), afterDelete.AddDays(30).AddMinutes(1));
    }

    [Fact]
    public async Task CreateTenant_WithNonAdmin_ReturnsForbidden()
    {
        var client = factory.CreateAuthenticatedClient(roles: ["user"]);

        var response = await client.PostAsJsonAsync("/api/tenants", new
        {
            Name = "Forbidden",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private sealed record TenantResponse(
        Guid Id,
        string Name,
        string Slug,
        TenantStatus Status,
        int DefaultRateLimit,
        DateTimeOffset CreatedAt,
        DateTimeOffset? OffboardedAt,
        DateTimeOffset? DataRetentionUntil);
}
