namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using ImageGenerationId = nem.Contracts.Identity.ImageGenerationId;
using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

internal sealed class ImageGenerationRepository(MimirDbContext context) : IImageGenerationRepository
{
    public async Task<ImageGeneration?> GetByIdAsync(ImageGenerationId id, CancellationToken cancellationToken = default)
    {
        return await context.ImageGenerations
            .AsNoTracking()
            .FirstOrDefaultAsync(image => image.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PaginatedList<ImageGeneration>> GetByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.ImageGenerations
            .AsNoTracking()
            .Where(image => image.UserId == userId)
            .OrderByDescending(image => image.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<ImageGeneration>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

    public async Task<ImageGeneration> CreateAsync(ImageGeneration imageGeneration, CancellationToken cancellationToken = default)
    {
        await context.ImageGenerations.AddAsync(imageGeneration, cancellationToken).ConfigureAwait(false);
        return imageGeneration;
    }

    public Task UpdateAsync(ImageGeneration imageGeneration, CancellationToken cancellationToken = default)
    {
        context.ImageGenerations.Update(imageGeneration);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(ImageGenerationId id, CancellationToken cancellationToken = default)
    {
        var imageGeneration = await context.ImageGenerations.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (imageGeneration is not null)
        {
            context.ImageGenerations.Remove(imageGeneration);
        }
    }
}
