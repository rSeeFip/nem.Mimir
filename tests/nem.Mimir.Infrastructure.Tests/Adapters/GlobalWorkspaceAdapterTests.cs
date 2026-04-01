namespace nem.Mimir.Infrastructure.Tests.Adapters;

using MediatR;
using Microsoft.Extensions.Options;
using nem.Contracts.Cognitive;
using nem.Contracts.Identity;
using nem.MCP.Core.Cognitive;
using nem.Mimir.Infrastructure.Adapters;
using NSubstitute;
using Shouldly;
using Wolverine;

public sealed class GlobalWorkspaceAdapterTests
{
    [Fact]
    public async Task Handle_PublishesWorkspaceBroadcastNotificationToMediator()
    {
        var mediator = Substitute.For<IMediator>();
        var evt = new WorkspaceBroadcastEvent(
            CoalitionId: Guid.NewGuid(),
            EntryIds: [WorkspaceEntryId.New()],
            CohesionReason: "test",
            AggregateSalience: 0.8,
            BroadcastAt: DateTimeOffset.UtcNow,
            Entries:
            [
                new WorkspaceEntry(WorkspaceEntryId.New(), "hello", 0.7, DateTimeOffset.UtcNow, "global-workspace"),
            ]);

        await GlobalWorkspaceAdapter.Handle(evt, mediator, CancellationToken.None);

        await mediator.Received(1).Publish(
            Arg.Is<WorkspaceBroadcastNotification>(n => n.BroadcastEvent == evt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WorkspaceBroadcastNotificationHandler_OnlyDispatchesToOptInActiveAgents()
    {
        var participatingAgent = Substitute.For<ICognitiveAgent>();
        participatingAgent.ServiceName.Returns("planner");
        participatingAgent.IsActive.Returns(true);

        var nonParticipatingAgent = Substitute.For<ICognitiveAgent>();
        nonParticipatingAgent.ServiceName.Returns("observer");
        nonParticipatingAgent.IsActive.Returns(true);

        var inactiveAgent = Substitute.For<ICognitiveAgent>();
        inactiveAgent.ServiceName.Returns("summarizer");
        inactiveAgent.IsActive.Returns(false);

        var options = Options.Create(new GlobalWorkspaceAdapterOptions());
        options.Value.ParticipatingAgentServiceNames.Add("planner");

        var handler = new WorkspaceBroadcastNotificationHandler(
            [participatingAgent, nonParticipatingAgent, inactiveAgent],
            options);

        var entryA = new WorkspaceEntry(WorkspaceEntryId.New(), "entry-a", 0.5, DateTimeOffset.UtcNow, "mcp");
        var entryB = new WorkspaceEntry(WorkspaceEntryId.New(), "entry-b", 0.6, DateTimeOffset.UtcNow, "mcp");
        var notification = new WorkspaceBroadcastNotification(new WorkspaceBroadcastEvent(
            CoalitionId: Guid.NewGuid(),
            EntryIds: [entryA.EntryId, entryB.EntryId],
            CohesionReason: "coalition",
            AggregateSalience: 0.55,
            BroadcastAt: DateTimeOffset.UtcNow,
            Entries: [entryA, entryB]));

        await handler.Handle(notification, CancellationToken.None);

        await participatingAgent.Received(1).OnWorkspaceEntryAsync(entryA, Arg.Any<CancellationToken>());
        await participatingAgent.Received(1).OnWorkspaceEntryAsync(entryB, Arg.Any<CancellationToken>());
        await nonParticipatingAgent.DidNotReceive().OnWorkspaceEntryAsync(Arg.Any<WorkspaceEntry>(), Arg.Any<CancellationToken>());
        await inactiveAgent.DidNotReceive().OnWorkspaceEntryAsync(Arg.Any<WorkspaceEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MimirWorkspaceResponseHandler_PublishesWorkspaceEntryToMessageBus()
    {
        var messageBus = Substitute.For<IMessageBus>();
        var handler = new MimirWorkspaceResponseHandler(messageBus);
        var entry = new WorkspaceEntry(WorkspaceEntryId.New(), "agent-response", 0.9, DateTimeOffset.UtcNow, "mimir-agent");

        await handler.Handle(new MimirWorkspaceResponseNotification(entry), CancellationToken.None);

        await messageBus.Received(1).PublishAsync(entry);
    }
}
