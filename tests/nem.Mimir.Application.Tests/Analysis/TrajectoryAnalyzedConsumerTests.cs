using nem.Contracts.Events.Integration;
using nem.Contracts.Identity;
using nem.Mimir.Application.Analysis;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace nem.Mimir.Application.Tests.Analysis;

public sealed class TrajectoryAnalyzedConsumerTests
{
    [Fact]
    public async Task Handle_Succeeded_Event_Should_Skip_Analysis()
    {
        // Arrange
        var analyzer = Substitute.For<ITrajectoryAnalyzer>();
        var bus = Substitute.For<IMessageBus>();
        var @event = new TrajectoryRecordedEvent(
            TrajectoryId.New(),
            SkillId.New(),
            SkillVersionId.New(),
            Succeeded: true,
            RecordedAt: DateTimeOffset.UtcNow);

        // Act
        await TrajectoryAnalyzedConsumer.Handle(@event, analyzer, bus, CancellationToken.None);

        // Assert
        await analyzer.DidNotReceive().AnalyzeAsync(Arg.Any<TrajectoryId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Failed_Event_Should_Call_Analyzer()
    {
        // Arrange
        var analyzer = Substitute.For<ITrajectoryAnalyzer>();
        var bus = Substitute.For<IMessageBus>();
        var trajectoryId = TrajectoryId.New();
        var @event = new TrajectoryRecordedEvent(
            trajectoryId,
            SkillId.New(),
            SkillVersionId.New(),
            Succeeded: false,
            RecordedAt: DateTimeOffset.UtcNow);

        // Act
        await TrajectoryAnalyzedConsumer.Handle(@event, analyzer, bus, CancellationToken.None);

        // Assert
        await analyzer.Received(1).AnalyzeAsync(trajectoryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Analyzer_Returns_Event_Should_Publish()
    {
        // Arrange
        var analyzer = Substitute.For<ITrajectoryAnalyzer>();
        var bus = Substitute.For<IMessageBus>();
        var trajectoryId = TrajectoryId.New();
        var @event = new TrajectoryRecordedEvent(
            trajectoryId,
            SkillId.New(),
            SkillVersionId.New(),
            Succeeded: false,
            RecordedAt: DateTimeOffset.UtcNow);

        var suggested = new EvolutionSuggestedEvent(
            AnalysisResultId.New(),
            SkillId.Empty,
            trajectoryId,
            "FIX",
            "tool call failed repeatedly",
            DateTimeOffset.UtcNow);

        analyzer.AnalyzeAsync(trajectoryId, Arg.Any<CancellationToken>())
            .Returns(suggested);

        // Act
        await TrajectoryAnalyzedConsumer.Handle(@event, analyzer, bus, CancellationToken.None);

        // Assert
        await bus.Received(1).PublishAsync(suggested);
    }

    [Fact]
    public async Task Handle_Analyzer_Returns_Null_Should_Not_Publish()
    {
        // Arrange
        var analyzer = Substitute.For<ITrajectoryAnalyzer>();
        var bus = Substitute.For<IMessageBus>();
        var trajectoryId = TrajectoryId.New();
        var @event = new TrajectoryRecordedEvent(
            trajectoryId,
            SkillId.New(),
            SkillVersionId.New(),
            Succeeded: false,
            RecordedAt: DateTimeOffset.UtcNow);

        analyzer.AnalyzeAsync(trajectoryId, Arg.Any<CancellationToken>())
            .Returns((EvolutionSuggestedEvent?)null);

        // Act
        await TrajectoryAnalyzedConsumer.Handle(@event, analyzer, bus, CancellationToken.None);

        // Assert
        await bus.DidNotReceive().PublishAsync(Arg.Any<EvolutionSuggestedEvent>());
        true.ShouldBeTrue();
    }
}
