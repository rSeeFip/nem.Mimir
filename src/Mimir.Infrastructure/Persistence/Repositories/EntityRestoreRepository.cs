using Microsoft.EntityFrameworkCore;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Common;
using Mimir.Domain.Entities;
using Mimir.Domain.ValueObjects;

namespace Mimir.Infrastructure.Persistence.Repositories;

internal sealed class EntityRestoreRepository(MimirDbContext context) : IEntityRestoreRepository
{
    public async Task<object?> GetByIdIncludingDeletedAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        return entityType switch
        {
            "conversation" => await context.Conversations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken),
            "user" => await context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken),
            "systemprompt" => await context.SystemPrompts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == SystemPromptId.From(entityId), cancellationToken),
            _ => null,
        };
    }

    public void Restore(object entity)
    {
        var entry = context.Entry(entity);
        entry.Property("IsDeleted").CurrentValue = false;
        entry.Property("DeletedAt").CurrentValue = null;
        entry.State = EntityState.Modified;
    }
}
