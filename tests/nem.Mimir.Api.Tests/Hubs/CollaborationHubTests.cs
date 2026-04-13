using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using nem.Mimir.Api.Hubs;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Notes.Services;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace nem.Mimir.Api.Tests.Hubs;

public sealed class CollaborationHubTests : IDisposable
{
    public CollaborationHubTests()
    {
        ClearHubState();
    }

    public void Dispose()
    {
        ClearHubState();
    }

    [Fact]
    public async Task JoinDocument_AddsGroup_AndSendsCurrentState()
    {
        var store = CreateStore(initialDocument: new AutomergeDocumentStoreDocument
        {
            Id = "automerge:doc-1",
            DocumentId = "doc-1",
            State = new byte[] { 0xAA, 0xBB },
        });

        var fixture = CreateHubFixture(store, "conn-join");

        await fixture.Hub.OnConnectedAsync();
        await fixture.Hub.JoinDocument("doc-1");

        await fixture.Groups.Received(1)
            .AddToGroupAsync("conn-join", "document:doc-1", Arg.Any<CancellationToken>());

        await fixture.Caller.Received(1).DocumentState("doc-1", Arg.Is<byte[]>(x => x.SequenceEqual(new byte[] { 0xAA, 0xBB })));
    }

    [Fact]
    public async Task SyncDocument_AppliesChanges_AndBroadcastsToOthers()
    {
        var session = Substitute.For<Marten.IDocumentSession>();
        AutomergeDocumentStoreDocument? stored = new()
        {
            Id = "automerge:doc-1",
            DocumentId = "doc-1",
            State = new byte[] { 0x01 },
        };

        session.LoadAsync<AutomergeDocumentStoreDocument>("automerge:doc-1", Arg.Any<CancellationToken>())
            .Returns(_ => stored);
        session.When(x => x.Store(Arg.Any<object>()))
            .Do(call => stored = (AutomergeDocumentStoreDocument)call.Args()[0]);

        var store = new AutomergeDocumentStore(session, TimeProvider.System);

        var fixture = CreateHubFixture(store, "conn-sync");

        await fixture.Hub.OnConnectedAsync();

        var syncMessage = new byte[] { 0x10, 0x20 };
        await fixture.Hub.SyncDocument("doc-1", syncMessage);

        stored.ShouldNotBeNull();
        stored!.State.SequenceEqual(syncMessage).ShouldBeTrue();
        await fixture.Others.Received(1).DocumentSync("doc-1", Arg.Is<byte[]>(x => x.SequenceEqual(syncMessage)));
    }

    [Fact]
    public async Task GetDocumentPresence_ReturnsUniqueUsersInDocument()
    {
        var store = CreateStore();

        var fixture1 = CreateHubFixture(store, "conn-u1", userId: Guid.NewGuid(), userName: "owner");
        var fixture2 = CreateHubFixture(store, "conn-u2", userId: Guid.NewGuid(), userName: "editor");

        await fixture1.Hub.OnConnectedAsync();
        await fixture2.Hub.OnConnectedAsync();

        await fixture1.Hub.JoinDocument("doc-presence");
        await fixture2.Hub.JoinDocument("doc-presence");

        var presence = await fixture1.Hub.GetDocumentPresence("doc-presence");

        presence.Count.ShouldBe(2);
        presence.Select(x => x.UserName).ShouldBe(["owner", "editor"], ignoreOrder: true);
    }

    private static AutomergeDocumentStore CreateStore(AutomergeDocumentStoreDocument? initialDocument = null)
    {
        var session = Substitute.For<Marten.IDocumentSession>();
        AutomergeDocumentStoreDocument? stored = initialDocument;

        session.LoadAsync<AutomergeDocumentStoreDocument>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.ArgAt<string>(0);
                return stored?.Id == id ? stored : null;
            });

        session.When(x => x.Store(Arg.Any<object>()))
            .Do(call => stored = (AutomergeDocumentStoreDocument)call.Args()[0]);

        return new AutomergeDocumentStore(session, TimeProvider.System);
    }

    private static HubFixture CreateHubFixture(AutomergeDocumentStore store, string connectionId, Guid? userId = null, string userName = "test-user")
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns((userId ?? Guid.NewGuid()).ToString());

        var hub = new CollaborationHub(
            currentUserService,
            Substitute.For<IMessageBus>(),
            Substitute.For<IChannelRepository>(),
            store,
            NullLogger<CollaborationHub>.Instance);

        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(connectionId);
        context.ConnectionAborted.Returns(CancellationToken.None);
        context.User.Returns(new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, userName)])));

        var groups = Substitute.For<IGroupManager>();

        var clients = Substitute.For<IHubCallerClients<ICollaborationHubClient>>();
        var caller = Substitute.For<ICollaborationHubClient>();
        var others = Substitute.For<ICollaborationHubClient>();
        var group = Substitute.For<ICollaborationHubClient>();

        clients.Caller.Returns(caller);
        clients.OthersInGroup(Arg.Any<string>()).Returns(others);
        clients.Group(Arg.Any<string>()).Returns(group);
        clients.All.Returns(group);

        SetHubProperties(hub, context, groups, clients);

        return new HubFixture(hub, groups, caller, others);
    }

    private static void SetHubProperties(CollaborationHub hub, HubCallerContext context, IGroupManager groups, IHubCallerClients<ICollaborationHubClient> clients)
    {
        var hubType = typeof(Hub<ICollaborationHubClient>);
        hubType.GetProperty(nameof(Hub.Context))!.SetValue(hub, context);
        hubType.GetProperty(nameof(Hub.Groups))!.SetValue(hub, groups);
        hubType.GetProperty(nameof(Hub<ICollaborationHubClient>.Clients))!.SetValue(hub, clients);
    }

    private static void ClearHubState()
    {
        var type = typeof(CollaborationHub);
        ClearDictionaryField(type, "ConnectionUsers");
        ClearDictionaryField(type, "ChannelConnections");
        ClearDictionaryField(type, "DocumentConnections");
    }

    private static void ClearDictionaryField(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        var value = field.GetValue(null)
            ?? throw new InvalidOperationException($"Field '{fieldName}' value is null.");

        value.GetType().GetMethod("Clear")?.Invoke(value, null);
    }

    private sealed record HubFixture(
        CollaborationHub Hub,
        IGroupManager Groups,
        ICollaborationHubClient Caller,
        ICollaborationHubClient Others);
}
