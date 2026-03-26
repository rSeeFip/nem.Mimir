namespace nem.Mimir.Domain.Events;

using NoteTypedId = nem.Contracts.Identity.NoteId;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;

public sealed record CollaboratorAddedEvent(NoteTypedId NoteId, Guid UserId, NotePermission Permission) : IDomainEvent;
