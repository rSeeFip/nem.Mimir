using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using ImageGenerationId = nem.Contracts.Identity.ImageGenerationId;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IImageGenerationRepository
{
    Task<ImageGeneration?> GetByIdAsync(ImageGenerationId id, CancellationToken cancellationToken = default);

    Task<PaginatedList<ImageGeneration>> GetByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ImageGeneration> CreateAsync(ImageGeneration imageGeneration, CancellationToken cancellationToken = default);

    Task UpdateAsync(ImageGeneration imageGeneration, CancellationToken cancellationToken = default);

    Task DeleteAsync(ImageGenerationId id, CancellationToken cancellationToken = default);
}
