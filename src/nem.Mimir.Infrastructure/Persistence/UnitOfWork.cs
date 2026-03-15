namespace nem.Mimir.Infrastructure.Persistence;

using nem.Mimir.Application.Common.Interfaces;

internal sealed class UnitOfWork(MimirDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
