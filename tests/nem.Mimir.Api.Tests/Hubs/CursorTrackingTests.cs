using System.Collections.Concurrent;
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

public sealed class CursorTrackingTests : IDisposable
{
    public CursorTrackingTests()
    {
        ClearHubState();
    }

    public void Dispose()
    {
        ClearHubState();
    }

    [Fact]
    public async Task CursorMoved_BroadcastsToDocumentGroup()
    {
        var userId = Guid.NewGuid();
        var fixture = CreateHubFixture(CreateStore(), "conn-cursor", userId, "editor");

        await fixture.Hub.OnConnectedAsync();
        await fixture.Hub.JoinDocument("doc-1");

        await fixture.Hub.CursorMoved("doc-1", userId.ToString(), 12, "8:12");

        await fixture.Others.Received(1).UserCursorMoved(Arg.Is<DocumentCursorDto>(cursor =>
            cursor.DocumentId == "doc-1"
            && cursor.UserId == userId.ToString()
            && cursor.UserName == "editor"
            && cursor.Position == 12
            && cursor.SelectionRange == "8:12"));
    }

    [Fact]
    public async Task CursorLeft_RemovesCursor()
    {
        var userId = Guid.NewGuid();
        var fixture = CreateHubFixture(CreateStore(), "conn-left", userId, "editor");

        await fixture.Hub.OnConnectedAsync();
        await fixture.Hub.JoinDocument("doc-1");
        await fixture.Hub.CursorMoved("doc-1", userId.ToString(), 15, null);

        await fixture.Hub.CursorLeft("doc-1", userId.ToString());

        await fixture.Group.Received(1).UserCursorLeft(Arg.Is<DocumentCursorLeftDto>(cursor =>
            cursor.DocumentId == "doc-1" && cursor.UserId == userId.ToString()));

        var cursors = GetCursorState();
        cursors.ContainsKey("doc-1").ShouldBeFalse();
    }

    [Fact]
    public async Task Disconnect_TriggersCursorCleanup()
    {
        var userId = Guid.NewGuid();
        var fixture = CreateHubFixture(CreateStore(), "conn-disconnect", userId, "editor");

        await fixture.Hub.OnConnectedAsync();
        await fixture.Hub.JoinDocument("doc-1");
        await fixture.Hub.CursorMoved("doc-1", userId.ToString(), 5, null);

        await fixture.Hub.OnDisconnectedAsync(null);

        await fixture.Group.Received(1).UserCursorLeft(Arg.Is<DocumentCursorLeftDto>(cursor =>
            cursor.DocumentId == "doc-1" && cursor.UserId == userId.ToString()));

        var cursors = GetCursorState();
        cursors.TryGetValue("doc-1", out var documentCursors).ShouldBeFalse();
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

    private static HubFixture CreateHubFixture(AutomergeDocumentStore store, string connectionId, Guid userId, string userName)
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(userId.ToString());

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

        return new HubFixture(hub, caller, others, group);
    }

    private static void SetHubProperties(CollaborationHub hub, HubCallerContext context, IGroupManager groups, IHubCallerClients<ICollaborationHubClient> clients)
    {
        var hubType = typeof(Hub<ICollaborationHubClient>);
        FindHubProperty(hubType, nameof(Hub.Context), typeof(HubCallerContext)).SetValue(hub, context);
        FindHubProperty(hubType, nameof(Hub.Groups), typeof(IGroupManager)).SetValue(hub, groups);
        FindHubProperty(hubType, nameof(Hub<ICollaborationHubClient>.Clients), typeof(IHubCallerClients<ICollaborationHubClient>)).SetValue(hub, clients);
    }

    private static PropertyInfo FindHubProperty(Type hubType, string name, Type propertyType) => hubType
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Single(property => property.Name == name && property.PropertyType == propertyType);

    private static ConcurrentDictionary<string, ConcurrentDictionary<string, CursorPosition>> GetCursorState()
    {
        var field = typeof(CollaborationHub).GetField("DocumentCursors", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Field 'DocumentCursors' was not found.");

        return (ConcurrentDictionary<string, ConcurrentDictionary<string, CursorPosition>>)(field.GetValue(null)
            ?? throw new InvalidOperationException("Field 'DocumentCursors' value is null."));
    }

    private static void ClearHubState()
    {
        var type = typeof(CollaborationHub);
        ClearDictionaryField(type, "ConnectionUsers");
        ClearDictionaryField(type, "ChannelConnections");
        ClearDictionaryField(type, "DocumentConnections");
        ClearDictionaryField(type, "DocumentCursors");
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
        ICollaborationHubClient Caller,
        ICollaborationHubClient Others,
        ICollaborationHubClient Group);
}
