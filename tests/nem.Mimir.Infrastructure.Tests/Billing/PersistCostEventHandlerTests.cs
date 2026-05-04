using Marten;
using nem.Contracts.Costs;
using nem.Mimir.Infrastructure.Billing;
using Shouldly;
using Testcontainers.PostgreSql;

namespace nem.Mimir.Infrastructure.Tests.Billing;

public class PersistCostEventHandlerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithImage("postgres:16-alpine")
        .Build();

    private IDocumentStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_postgres.GetConnectionString());
            opts.Schema.For<PersistedCostEvent>()
                .UniqueIndex(x => x.IdempotencyKey);
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.CreateOrUpdate;
        });
    }

    public async ValueTask DisposeAsync()
    {
        _store.Dispose();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PersistsCostEvent()
    {
        var occurredAt = new DateTimeOffset(2026, 1, 20, 10, 30, 0, TimeSpan.Zero);
        var costEvent = CreateCostEvent(occurredAt: occurredAt);
        var sut = new PersistCostEventHandler();

        await using (var session = _store.LightweightSession())
        {
            await sut.Handle(costEvent, session);
        }

        await using var querySession = _store.QuerySession();
        var persistedEvents = await querySession.Query<PersistedCostEvent>().ToListAsync();

        persistedEvents.Count.ShouldBe(1);
        persistedEvents[0].OccurredAt.ShouldBe(occurredAt);
    }

    [Fact]
    public async Task Handle_SameComputedIdempotencyKey_DoesNotCreateDuplicate()
    {
        var occurredAt = new DateTimeOffset(2026, 1, 20, 10, 30, 0, TimeSpan.Zero);
        var first = CreateCostEvent(idempotencyKey: "incoming-key-1", occurredAt: occurredAt);
        var retry = CreateCostEvent(idempotencyKey: "incoming-key-2", occurredAt: occurredAt);
        var sut = new PersistCostEventHandler();

        await using (var session = _store.LightweightSession())
        {
            await sut.Handle(first, session);
        }

        await using (var session = _store.LightweightSession())
        {
            await sut.Handle(retry, session);
        }

        await using var querySession = _store.QuerySession();
        var persistedEvents = await querySession.Query<PersistedCostEvent>().ToListAsync();

        persistedEvents.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_MapsFieldsCorrectly()
    {
        var occurredAt = new DateTimeOffset(2026, 2, 3, 15, 45, 0, TimeSpan.Zero);
        var costEvent = CreateCostEvent(
            tenantId: "tenant-42",
            userId: "user-99",
            model: "gpt-4o-mini",
            promptTokens: 321,
            completionTokens: 123,
            totalTokens: 444,
            costUsd: 1.23m,
            channel: "telegram",
            occurredAt: occurredAt,
            idempotencyKey: "transport-key");
        var sut = new PersistCostEventHandler();

        await using (var session = _store.LightweightSession())
        {
            await sut.Handle(costEvent, session);
        }

        await using var querySession = _store.QuerySession();
        var persisted = await querySession.Query<PersistedCostEvent>().SingleAsync();

        persisted.TenantId.ShouldBe("tenant-42");
        persisted.UserId.ShouldBe("user-99");
        persisted.Model.ShouldBe("gpt-4o-mini");
        persisted.PromptTokens.ShouldBe(321);
        persisted.CompletionTokens.ShouldBe(123);
        persisted.TotalTokens.ShouldBe(444);
        persisted.CostUsd.ShouldBe(1.23m);
        persisted.Channel.ShouldBe("telegram");
        persisted.OccurredAt.ShouldBe(occurredAt);
        persisted.IdempotencyKey.ShouldBe(
            PersistedCostEvent.ComputeIdempotencyKey("tenant-42", "user-99", "gpt-4o-mini", occurredAt));
        persisted.IdempotencyKey.ShouldNotBe("transport-key");
    }

    private static CostEvent CreateCostEvent(
        string tenantId = "tenant-1",
        string userId = "user-1",
        string model = "gpt-4o",
        int promptTokens = 100,
        int completionTokens = 50,
        int? totalTokens = null,
        decimal costUsd = 0.42m,
        string channel = "api",
        DateTimeOffset? occurredAt = null,
        string idempotencyKey = "transport-key")
    {
        var timestamp = occurredAt ?? new DateTimeOffset(2026, 1, 20, 10, 30, 0, TimeSpan.Zero);
        var total = totalTokens ?? (promptTokens + completionTokens);

        return new CostEvent
        {
            IdempotencyKey = idempotencyKey,
            Timestamp = timestamp,
            ServiceId = "nem.mimir",
            TenantId = tenantId,
            ResourceType = CostResourceType.LlmInference,
            UsageQuantity = total,
            UsageUnit = "tokens",
            RawCost = costUsd,
            AmortizedCost = costUsd,
            Currency = "USD",
            Tags = new Dictionary<string, string>
            {
                ["user_id"] = userId,
                ["model_name"] = model,
                ["input_tokens"] = promptTokens.ToString(),
                ["output_tokens"] = completionTokens.ToString(),
                ["total_tokens"] = total.ToString(),
                ["channel"] = channel,
            },
        };
    }
}
