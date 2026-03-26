using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using nem.Mimir.Api.Hubs;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Notes.Services;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace nem.Mimir.Api.Tests.Hubs;

public sealed class NoteCollaborationTests : IDisposable
{
    public NoteCollaborationTests()
    {
        ClearHubState();
    }

    public void Dispose()
    {
        ClearHubState();
    }

    [Fact]
    public async Task JoinNote_UserWithoutViewPermission_ThrowsHubException()
    {
        var noteRepository = Substitute.For<INoteRepository>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        currentUserService.UserId.Returns(userId.ToString());

        var note = Note.Create(ownerId, "private", [0x01]);
        noteRepository.GetWithCollaboratorsAsync(Arg.Any<nem.Contracts.Identity.NoteId>(), Arg.Any<CancellationToken>())
            .Returns(note);

        var yjsStore = new YjsDocumentStore(noteRepository, currentUserService, unitOfWork);
        var fixture = CreateHubFixture(currentUserService, noteRepository, yjsStore, "conn-denied");

        await fixture.Hub.OnConnectedAsync();

        var ex = await Should.ThrowAsync<HubException>(() => fixture.Hub.JoinNote(note.Id.Value.ToString("D")));
        ex.Message.ShouldBe("User does not have permission to view this note.");
    }

    [Fact]
    public async Task JoinNote_ValidUser_AddsGroup_AndSendsCurrentState()
    {
        var noteRepository = Substitute.For<INoteRepository>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var ownerId = Guid.NewGuid();
        currentUserService.UserId.Returns(ownerId.ToString());

        var note = Note.Create(ownerId, "doc", [0xAA, 0xBB]);

        noteRepository.GetWithCollaboratorsAsync(Arg.Any<nem.Contracts.Identity.NoteId>(), Arg.Any<CancellationToken>())
            .Returns(note);
        noteRepository.GetByIdAsync(Arg.Any<nem.Contracts.Identity.NoteId>(), Arg.Any<CancellationToken>())
            .Returns(note);

        var yjsStore = new YjsDocumentStore(noteRepository, currentUserService, unitOfWork);
        var fixture = CreateHubFixture(currentUserService, noteRepository, yjsStore, "conn-join");

        await fixture.Hub.OnConnectedAsync();
        await fixture.Hub.JoinNote(note.Id.Value.ToString("D"));

        await fixture.Groups.Received(1)
            .AddToGroupAsync("conn-join", $"note:{note.Id.Value:D}", Arg.Any<CancellationToken>());

        await fixture.CallerProxy.Received(1)
            .SendCoreAsync("NoteState", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncNote_EditorUser_AppliesUpdate_AndBroadcastsToOthers()
    {
        var noteRepository = Substitute.For<INoteRepository>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var ownerId = Guid.NewGuid();
        var editorId = Guid.NewGuid();
        currentUserService.UserId.Returns(editorId.ToString());

        var note = Note.Create(ownerId, "shared", [0x01]);
        note.AddCollaborator(editorId, NotePermission.Editor);

        noteRepository.GetWithCollaboratorsAsync(Arg.Any<nem.Contracts.Identity.NoteId>(), Arg.Any<CancellationToken>())
            .Returns(note);

        var yjsStore = new YjsDocumentStore(noteRepository, currentUserService, unitOfWork);
        var fixture = CreateHubFixture(currentUserService, noteRepository, yjsStore, "conn-sync");

        await fixture.Hub.OnConnectedAsync();

        var delta = new byte[] { 0x02, 0x03 };
        await fixture.Hub.SyncNote(note.Id.Value.ToString("D"), delta);

        await noteRepository.Received(1).UpdateAsync(note, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await fixture.OthersProxy.Received(1)
            .SendCoreAsync("NoteSync", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetNotePresence_ReturnsUniqueUsersInNote()
    {
        var noteRepository = Substitute.For<INoteRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var noteOwner = Guid.NewGuid();
        var noteCollaborator = Guid.NewGuid();

        var note = Note.Create(noteOwner, "team-note", [0x11]);
        note.AddCollaborator(noteCollaborator, NotePermission.Viewer);

        noteRepository.GetWithCollaboratorsAsync(Arg.Any<nem.Contracts.Identity.NoteId>(), Arg.Any<CancellationToken>())
            .Returns(note);
        noteRepository.GetByIdAsync(Arg.Any<nem.Contracts.Identity.NoteId>(), Arg.Any<CancellationToken>())
            .Returns(note);

        var currentUser1 = Substitute.For<ICurrentUserService>();
        currentUser1.UserId.Returns(noteOwner.ToString());
        var store1 = new YjsDocumentStore(noteRepository, currentUser1, unitOfWork);
        var fixture1 = CreateHubFixture(currentUser1, noteRepository, store1, "conn-u1", "owner");

        var currentUser2 = Substitute.For<ICurrentUserService>();
        currentUser2.UserId.Returns(noteCollaborator.ToString());
        var store2 = new YjsDocumentStore(noteRepository, currentUser2, unitOfWork);
        var fixture2 = CreateHubFixture(currentUser2, noteRepository, store2, "conn-u2", "viewer");

        await fixture1.Hub.OnConnectedAsync();
        await fixture2.Hub.OnConnectedAsync();

        var noteId = note.Id.Value.ToString("D");
        await fixture1.Hub.JoinNote(noteId);
        await fixture2.Hub.JoinNote(noteId);

        var presence = await fixture1.Hub.GetNotePresence(noteId);

        presence.Count.ShouldBe(2);
        presence.ShouldContain(p => p.UserId == noteOwner);
        presence.ShouldContain(p => p.UserId == noteCollaborator);
    }

    private static (CollaborationHub Hub, IGroupManager Groups, ISingleClientProxy CallerProxy, IClientProxy OthersProxy) CreateHubFixture(
        ICurrentUserService currentUserService,
        INoteRepository noteRepository,
        YjsDocumentStore yjsDocumentStore,
        string connectionId,
        string userName = "test-user")
    {
        var bus = Substitute.For<IMessageBus>();
        var channelRepository = Substitute.For<IChannelRepository>();

        var hub = new CollaborationHub(
            currentUserService,
            bus,
            channelRepository,
            noteRepository,
            yjsDocumentStore,
            NullLogger<CollaborationHub>.Instance);

        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(connectionId);
        context.ConnectionAborted.Returns(CancellationToken.None);
        context.User.Returns(new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, userName)])));

        var groups = Substitute.For<IGroupManager>();

        var clients = Substitute.For<IHubCallerClients>();
        var callerProxy = Substitute.For<ISingleClientProxy>();
        var othersProxy = Substitute.For<IClientProxy>();
        var groupProxy = Substitute.For<IClientProxy>();

        clients.Caller.Returns(callerProxy);
        clients.OthersInGroup(Arg.Any<string>()).Returns(othersProxy);
        clients.Group(Arg.Any<string>()).Returns(groupProxy);

        SetHubProperties(hub, context, groups, clients);

        return (hub, groups, callerProxy, othersProxy);
    }

    private static void SetHubProperties(Hub hub, HubCallerContext context, IGroupManager groups, IHubCallerClients clients)
    {
        var hubType = typeof(Hub);
        hubType.GetProperty(nameof(Hub.Context))!.SetValue(hub, context);
        hubType.GetProperty(nameof(Hub.Groups))!.SetValue(hub, groups);
        hubType.GetProperty(nameof(Hub.Clients))!.SetValue(hub, clients);
    }

    private static void ClearHubState()
    {
        var type = typeof(CollaborationHub);
        ClearDictionaryField(type, "ConnectionUsers");
        ClearDictionaryField(type, "ChannelConnections");
        ClearDictionaryField(type, "NoteConnections");
    }

    private static void ClearDictionaryField(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        var value = field.GetValue(null)
            ?? throw new InvalidOperationException($"Field '{fieldName}' value is null.");

        value.GetType().GetMethod("Clear")?.Invoke(value, null);
    }
}
