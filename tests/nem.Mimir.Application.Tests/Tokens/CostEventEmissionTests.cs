using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Wolverine;
using nem.Contracts.Costs;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Tokens;

namespace nem.Mimir.Application.Tests.Tokens;

public sealed class CostEventEmissionTests
{
    private static readonly DateTimeOffset BaseTime = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static TokenTrackerService CreateSut(IMessageBus messageBus) =>
        new(new TokenTrackerOptions(), Substitute.For<ILogger<TokenTrackerService>>(), messageBus);

    [Fact]
    public async Task Emits_CostEvent_after_recording_usage()
    {
        var messageBus = Substitute.For<IMessageBus>();
        messageBus.PublishAsync(Arg.Any<CostEvent>()).Returns(ValueTask.CompletedTask);
        var sut = CreateSut(messageBus);

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 50, 0.05m, BaseTime), CancellationToken.None);

        await messageBus.Received(1).PublishAsync(Arg.Any<CostEvent>());
    }

    [Fact]
    public async Task CostEvent_has_LlmInference_resource_type_for_chat_model()
    {
        var messageBus = Substitute.For<IMessageBus>();
        CostEvent? captured = null;
        messageBus.When(x => x.PublishAsync(Arg.Any<CostEvent>())).Do(ci => captured = ci.Arg<CostEvent>());
        messageBus.PublishAsync(Arg.Any<CostEvent>()).Returns(ValueTask.CompletedTask);
        var sut = CreateSut(messageBus);

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 50, 0.05m, BaseTime), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.ResourceType.Should().Be(CostResourceType.LlmInference);
    }

    [Fact]
    public async Task CostEvent_has_Embedding_resource_type_for_embedding_model()
    {
        var messageBus = Substitute.For<IMessageBus>();
        CostEvent? captured = null;
        messageBus.When(x => x.PublishAsync(Arg.Any<CostEvent>())).Do(ci => captured = ci.Arg<CostEvent>());
        messageBus.PublishAsync(Arg.Any<CostEvent>()).Returns(ValueTask.CompletedTask);
        var sut = CreateSut(messageBus);

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "text-embedding-ada-002", 200, 0, 0.002m, BaseTime), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.ResourceType.Should().Be(CostResourceType.Embedding);
    }

    [Fact]
    public async Task CostEvent_idempotency_key_matches_pattern()
    {
        var messageBus = Substitute.For<IMessageBus>();
        CostEvent? captured = null;
        messageBus.When(x => x.PublishAsync(Arg.Any<CostEvent>())).Do(ci => captured = ci.Arg<CostEvent>());
        messageBus.PublishAsync(Arg.Any<CostEvent>()).Returns(ValueTask.CompletedTask);
        var sut = CreateSut(messageBus);

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 50, 0.05m, BaseTime), CancellationToken.None);

        var expectedBucket = BaseTime.ToString("yyyyMMddHHmm");
        captured!.IdempotencyKey.Should().Be($"mimir:svc-1:{expectedBucket}");
    }

    [Fact]
    public async Task CostEvent_tags_include_model_and_token_counts()
    {
        var messageBus = Substitute.For<IMessageBus>();
        CostEvent? captured = null;
        messageBus.When(x => x.PublishAsync(Arg.Any<CostEvent>())).Do(ci => captured = ci.Arg<CostEvent>());
        messageBus.PublishAsync(Arg.Any<CostEvent>()).Returns(ValueTask.CompletedTask);
        var sut = CreateSut(messageBus);

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o-mini", 120, 80, 0.01m, BaseTime), CancellationToken.None);

        captured!.Tags["model_name"].Should().Be("gpt-4o-mini");
        captured.Tags["input_tokens"].Should().Be("120");
        captured.Tags["output_tokens"].Should().Be("80");
        captured.Tags["total_tokens"].Should().Be("200");
    }

    [Fact]
    public async Task CostEvent_has_correct_service_and_currency()
    {
        var messageBus = Substitute.For<IMessageBus>();
        CostEvent? captured = null;
        messageBus.When(x => x.PublishAsync(Arg.Any<CostEvent>())).Do(ci => captured = ci.Arg<CostEvent>());
        messageBus.PublishAsync(Arg.Any<CostEvent>()).Returns(ValueTask.CompletedTask);
        var sut = CreateSut(messageBus);

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 50, 30, 0.03m, BaseTime), CancellationToken.None);

        captured!.ServiceId.Should().Be("nem.mimir");
        captured.Currency.Should().Be("USD");
        captured.UsageUnit.Should().Be("tokens");
        captured.RawCost.Should().Be(0.03m);
    }

    [Fact]
    public async Task Does_not_emit_when_messageBus_is_null()
    {
        var sut = new TokenTrackerService(new TokenTrackerOptions(), Substitute.For<ILogger<TokenTrackerService>>());

        var act = () => sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 50, 0.05m, BaseTime), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Does_not_emit_when_tracking_disabled()
    {
        var messageBus = Substitute.For<IMessageBus>();
        var options = new TokenTrackerOptions { EnableTracking = false };
        var sut = new TokenTrackerService(options, Substitute.For<ILogger<TokenTrackerService>>(), messageBus);

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 50, 0.05m, BaseTime), CancellationToken.None);

        await messageBus.DidNotReceive().PublishAsync(Arg.Any<CostEvent>());
    }
}
