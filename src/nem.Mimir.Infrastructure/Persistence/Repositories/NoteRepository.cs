namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using NoteId = nem.Contracts.Identity.NoteId;

internal sealed class NoteRepository(MimirDbContext context) : INoteRepository
{
    public async Task<Note?> GetByIdAsync(NoteId id, CancellationToken cancellationToken = default)
    {
        return await context.Notes
            .AsNoTracking()
            .FirstOrDefaultAsync(note => note.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Note?> GetWithCollaboratorsAsync(NoteId id, CancellationToken cancellationToken = default)
    {
        return await context.Notes
            .Include(note => note.Collaborators)
            .AsSplitQuery()
            .FirstOrDefaultAsync(note => note.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Note?> GetWithVersionsAsync(NoteId id, CancellationToken cancellationToken = default)
    {
        return await context.Notes
            .Include(note => note.Collaborators)
            .Include(note => note.Versions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(note => note.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PaginatedList<Note>> GetByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Notes
            .AsNoTracking()
            .Include(note => note.Collaborators)
            .Where(note => note.OwnerId == userId || note.Collaborators.Any(collaborator => collaborator.UserId == userId))
            .OrderByDescending(note => note.UpdatedAt ?? note.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<Note>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

    public async Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default)
    {
        await context.Notes.AddAsync(note, cancellationToken).ConfigureAwait(false);
        return note;
    }

    public Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
    {
        context.Notes.Update(note);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(NoteId id, CancellationToken cancellationToken = default)
    {
        var note = await context.Notes.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (note is not null)
        {
            context.Notes.Remove(note);
        }
    }

    public async Task<PaginatedList<NoteVersion>> GetVersionsAsync(
        NoteId noteId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.NoteVersions
            .AsNoTracking()
            .Where(version => version.NoteId == noteId)
            .OrderByDescending(version => version.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<NoteVersion>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }
}
