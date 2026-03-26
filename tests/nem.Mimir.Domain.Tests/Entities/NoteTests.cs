using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.Events;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public sealed class NoteTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_Note()
    {
        var ownerId = Guid.NewGuid();
        var content = new byte[] { 0x01, 0x02, 0x03 };

        var note = Note.Create(ownerId, "Architecture notes", content, ["design", "backend"]);

        note.OwnerId.ShouldBe(ownerId);
        note.Title.ShouldBe("Architecture notes");
        note.Content.ShouldBe(content);
        note.Tags.ShouldContain("design");
        note.Tags.ShouldContain("backend");
        note.Collaborators.ShouldContain(c => c.UserId == ownerId && c.Permission == NotePermission.Owner);
    }

    [Fact]
    public void Create_Should_Raise_NoteCreatedEvent()
    {
        var ownerId = Guid.NewGuid();

        var note = Note.Create(ownerId, "Daily notes", [0xFF]);

        var createdEvent = note.DomainEvents.OfType<NoteCreatedEvent>().ShouldHaveSingleItem();
        createdEvent.NoteId.ShouldBe(note.Id);
        createdEvent.OwnerId.ShouldBe(ownerId);
    }

    [Fact]
    public void Update_Should_Create_Version_And_Raise_NoteUpdatedEvent()
    {
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Plan", [0x01, 0x02]);

        note.Update(ownerId, "Plan v2", [0x03, 0x04], "Expanded plan");

        note.Title.ShouldBe("Plan v2");
        note.Content.ShouldBe(new byte[] { 0x03, 0x04 });
        note.Versions.Count.ShouldBe(1);
        note.Versions.First().ContentSnapshot.ShouldBe(new byte[] { 0x01, 0x02 });
        note.DomainEvents.OfType<NoteUpdatedEvent>().ShouldContain(e => e.UpdatedByUserId == ownerId);
    }

    [Fact]
    public void AddCollaborator_Should_Add_Collaborator_And_Raise_Event()
    {
        var ownerId = Guid.NewGuid();
        var collaboratorId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Team", [0x10]);

        note.AddCollaborator(collaboratorId, NotePermission.Editor);

        note.Collaborators.ShouldContain(c => c.UserId == collaboratorId && c.Permission == NotePermission.Editor);
        note.DomainEvents.OfType<CollaboratorAddedEvent>().ShouldContain(e => e.UserId == collaboratorId);
    }

    [Fact]
    public void RestoreVersion_Should_Replace_Content_With_Snapshot()
    {
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Doc", [0x11]);
        note.Update(ownerId, "Doc", [0x22], "first change");

        var firstVersion = note.Versions.First();
        note.RestoreVersion(ownerId, firstVersion.Id);

        note.Content.ShouldBe(new byte[] { 0x11 });
    }
}
