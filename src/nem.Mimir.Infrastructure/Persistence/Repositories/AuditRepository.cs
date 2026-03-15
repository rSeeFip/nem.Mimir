namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

internal sealed class AuditRepository(MimirDbContext context) : IAuditRepository
{
    public async Task<AuditEntry> CreateAsync(AuditEntry auditEntry, CancellationToken cancellationToken = default)
    {
        await context.AuditEntries
            .AddAsync(auditEntry, cancellationToken)
            .ConfigureAwait(false);

        return auditEntry;
    }

    public async Task<PaginatedList<AuditEntry>> GetByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.AuditEntries
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp);

        var totalCount = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<AuditEntry>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

    public async Task<PaginatedList<AuditEntry>> GetByEntityAsync(
        string entityType,
        string entityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.AuditEntries
            .AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp);

        var totalCount = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<AuditEntry>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

 public async Task<PaginatedList<AuditEntry>> GetAllAsync(
 int pageNumber,
 int pageSize,
 CancellationToken cancellationToken = default)
 {
 var query = context.AuditEntries
 .AsNoTracking()
 .OrderByDescending(a => a.Timestamp);

 var totalCount = await query
 .CountAsync(cancellationToken)
 .ConfigureAwait(false);

 var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

 var items = await query
 .Skip((pageNumber - 1) * pageSize)
 .Take(pageSize)
 .ToListAsync(cancellationToken)
 .ConfigureAwait(false);

 return new PaginatedList<AuditEntry>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
 }
}
