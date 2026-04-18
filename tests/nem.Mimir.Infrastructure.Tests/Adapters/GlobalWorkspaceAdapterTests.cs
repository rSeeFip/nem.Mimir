namespace nem.Mimir.Infrastructure.Tests.Adapters;

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using nem.Contracts.Cognitive;
using nem.Contracts.Identity;
using nem.MCP.Core.Cognitive;
using nem.Mimir.Infrastructure.Adapters;
using nem.Mimir.Application;
using NSubstitute;
using Shouldly;
using Wolverine;
using Wolverine.Runtime;

public sealed class GlobalWorkspaceAdapterTests
{
    [Fact]
    public async Task Handle_InvokesWorkspaceBroadcastHandlerThroughWolverine()
    {
        var messageBus = Substitute.For<IMessageBus>();
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

        await GlobalWorkspaceAdapter.Handle(evt, messageBus, CancellationToken.None);

        await messageBus.Received(1).InvokeAsync(
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
        var entry = new WorkspaceEntry(WorkspaceEntryId.New(), "agent-response", 0.9, DateTimeOffset.UtcNow, "mimir-agent");

        await MimirWorkspaceResponseHandler.Handle(new MimirWorkspaceResponseNotification(entry), messageBus, CancellationToken.None);

        await messageBus.Received(1).PublishAsync(entry);
    }

    [Fact]
    [Trait("Category", "Handlers")]
    public async Task HandlerGraph_IncludesWorkspaceAdapterHandlers()
    {
        var participatingAgent = Substitute.For<ICognitiveAgent>();
        participatingAgent.ServiceName.Returns("planner");
        participatingAgent.IsActive.Returns(true);

        using var host = await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(participatingAgent);
                services.Configure<GlobalWorkspaceAdapterOptions>(options =>
                {
                    options.ParticipatingAgentServiceNames.Add("planner");
                });
            })
            .UseWolverine(options =>
            {
                options.Discovery.IncludeAssembly(typeof(DependencyInjection).Assembly);
                options.Discovery.IncludeAssembly(typeof(GlobalWorkspaceAdapter).Assembly);
            })
            .StartAsync();

        var runtime = host.Services.GetRequiredService<IWolverineRuntime>();
        var handlerSnapshot = new
        {
            handler_count = runtime.Options.HandlerGraph.Chains.Count,
            handler_names = runtime.Options.HandlerGraph.Chains
                .Select(chain => chain.MessageType.FullName)
                .Where(name => name is not null)
                .OrderBy(name => name)
                .ToArray()
        };

        var evidencePath = Environment.GetEnvironmentVariable("NEM_MIMIR_HANDLER_COUNT_PATH")
            ?? "/workspace/.sisyphus/evidence/task-12-handler-count.txt";

        await File.WriteAllTextAsync(evidencePath, JsonSerializer.Serialize(handlerSnapshot), TestContext.Current.CancellationToken);

        handlerSnapshot.handler_names.ShouldContain(typeof(WorkspaceBroadcastEvent).FullName);
        handlerSnapshot.handler_names.ShouldContain(typeof(WorkspaceBroadcastNotification).FullName);
        handlerSnapshot.handler_names.ShouldContain(typeof(MimirWorkspaceResponseNotification).FullName);
    }
}
