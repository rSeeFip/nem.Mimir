using nem.Contracts.Identity;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IPromptTemplateRepository
{
    Task<PromptTemplate?> GetByIdAsync(PromptTemplateId id, CancellationToken cancellationToken = default);

    Task<PromptTemplate?> GetByIdForUserAsync(PromptTemplateId id, Guid userId, CancellationToken cancellationToken = default);

    Task<PromptTemplate?> GetByCommandForUserOrSharedAsync(string command, Guid userId, CancellationToken cancellationToken = default);

    Task<PromptTemplate?> GetByCommandForUserAsync(string command, Guid userId, CancellationToken cancellationToken = default);

    Task<PaginatedList<PromptTemplate>> GetAccessibleAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        string? search,
        bool? isShared,
        IReadOnlyCollection<string>? tags,
        CancellationToken cancellationToken = default);

    Task<PromptTemplate> CreateAsync(PromptTemplate template, CancellationToken cancellationToken = default);

    Task UpdateAsync(PromptTemplate template, CancellationToken cancellationToken = default);

    Task DeleteAsync(PromptTemplateId id, Guid userId, CancellationToken cancellationToken = default);
}
