using System.Reflection;
using Marten;
using Marten.Schema;
using nem.Mimir.Infrastructure.Billing;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Billing;

public class PersistedCostEventTests
{
    [Fact]
    public void PersistedCostEvent_HasAllRequiredFields()
    {
        var properties = typeof(PersistedCostEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToHashSet();

        properties.ShouldContain(nameof(PersistedCostEvent.Id));
        properties.ShouldContain(nameof(PersistedCostEvent.TenantId));
        properties.ShouldContain(nameof(PersistedCostEvent.UserId));
        properties.ShouldContain(nameof(PersistedCostEvent.Model));
        properties.ShouldContain(nameof(PersistedCostEvent.PromptTokens));
        properties.ShouldContain(nameof(PersistedCostEvent.CompletionTokens));
        properties.ShouldContain(nameof(PersistedCostEvent.TotalTokens));
        properties.ShouldContain(nameof(PersistedCostEvent.CostUsd));
        properties.ShouldContain(nameof(PersistedCostEvent.Channel));
        properties.ShouldContain(nameof(PersistedCostEvent.IdempotencyKey));
        properties.ShouldContain(nameof(PersistedCostEvent.OccurredAt));
    }

    [Fact]
    public void PersistedCostEvent_HasUniqueIndexOnIdempotencyKey()
    {
        using var store = DocumentStore.For(options =>
        {
            options.Connection("Host=localhost;Database=test;Username=test;Password=test");
            options.Schema.For<PersistedCostEvent>()
                .UniqueIndex(x => x.IdempotencyKey);
        });

        var storage = store.Options.Storage;
        var mapping = (DocumentMapping)storage.GetType()
            .GetMethod("MappingFor", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(storage, [typeof(PersistedCostEvent)])!;

        var index = mapping.Indexes.Cast<ComputedIndex>()
            .Single(x => x.IsUnique);

        index.IsUnique.ShouldBeTrue();
        index.Name.ShouldContain("idempotency");
    }

    [Fact]
    public void ComputeIdempotencyKey_ReturnsDeterministicHash()
    {
        var tenantId = "tenant-1";
        var userId = "user-42";
        var model = "gpt-4o";
        var occurredAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

        var key1 = PersistedCostEvent.ComputeIdempotencyKey(tenantId, userId, model, occurredAt);
        var key2 = PersistedCostEvent.ComputeIdempotencyKey(tenantId, userId, model, occurredAt);

        key1.ShouldBe(key2);
        key1.ShouldNotBeNullOrEmpty();
        key1.Length.ShouldBe(64);
    }

    [Fact]
    public void ComputeIdempotencyKey_DifferentInputsProduceDifferentKeys()
    {
        var occurredAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

        var key1 = PersistedCostEvent.ComputeIdempotencyKey("tenant-1", "user-1", "gpt-4o", occurredAt);
        var key2 = PersistedCostEvent.ComputeIdempotencyKey("tenant-1", "user-2", "gpt-4o", occurredAt);
        var key3 = PersistedCostEvent.ComputeIdempotencyKey("tenant-1", "user-1", "gpt-4o-mini", occurredAt);
        var key4 = PersistedCostEvent.ComputeIdempotencyKey("tenant-1", "user-1", "gpt-4o", occurredAt.AddSeconds(1));

        key1.ShouldNotBe(key2);
        key1.ShouldNotBe(key3);
        key1.ShouldNotBe(key4);
    }

    [Fact]
    public void PersistedCostEvent_DefaultValues_AreValid()
    {
        var evt = new PersistedCostEvent();

        evt.TenantId.ShouldBe(string.Empty);
        evt.UserId.ShouldBe(string.Empty);
        evt.Model.ShouldBe(string.Empty);
        evt.Channel.ShouldBe(string.Empty);
        evt.IdempotencyKey.ShouldBe(string.Empty);
        evt.PromptTokens.ShouldBe(0);
        evt.CompletionTokens.ShouldBe(0);
        evt.TotalTokens.ShouldBe(0);
        evt.CostUsd.ShouldBe(0m);
    }
}
