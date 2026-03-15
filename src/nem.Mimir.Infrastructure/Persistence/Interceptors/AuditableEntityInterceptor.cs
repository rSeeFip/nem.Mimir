namespace nem.Mimir.Infrastructure.Persistence.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Common;

public class AuditableEntityInterceptor(
    ICurrentUserService currentUserService,
    IDateTimeService dateTimeService) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context is null)
            return;

        var utcNow = dateTimeService.UtcNow;
        var userId = currentUserService.UserId;

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity<Guid>>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Property(nameof(BaseAuditableEntity<Guid>.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(BaseAuditableEntity<Guid>.DeletedAt)).CurrentValue = utcNow;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(BaseAuditableEntity<Guid>.CreatedAt)).CurrentValue = utcNow;
                entry.Property(nameof(BaseAuditableEntity<Guid>.CreatedBy)).CurrentValue = userId;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(nameof(BaseAuditableEntity<Guid>.UpdatedAt)).CurrentValue = utcNow;
                entry.Property(nameof(BaseAuditableEntity<Guid>.UpdatedBy)).CurrentValue = userId;
            }
        }
    }
}
