namespace nem.Mimir.Domain.Events;

using NoteTypedId = nem.Contracts.Identity.NoteId;
using nem.Mimir.Domain.Common;

public sealed record NoteCreatedEvent(NoteTypedId NoteId, Guid OwnerId) : IDomainEvent;
