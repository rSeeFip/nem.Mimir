using Microsoft.EntityFrameworkCore;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Common;
using Mimir.Domain.Entities;

namespace Mimir.Infrastructure.Persistence.Repositories;

internal sealed class EntityRestoreRepository(MimirDbContext context) : IEntityRestoreRepository
{
    public async Task<BaseAuditableEntity<Guid>?> GetByIdIncludingDeletedAsync(
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
                .FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken),
            _ => null,
        };
    }

    public void Restore(BaseAuditableEntity<Guid> entity)
    {
        var entry = context.Entry(entity);
        entry.Property(nameof(BaseAuditableEntity<Guid>.IsDeleted)).CurrentValue = false;
        entry.Property(nameof(BaseAuditableEntity<Guid>.DeletedAt)).CurrentValue = null;
        entry.State = EntityState.Modified;
    }
}
