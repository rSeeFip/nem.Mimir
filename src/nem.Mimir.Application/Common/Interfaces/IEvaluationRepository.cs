using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using EvaluationId = nem.Mimir.Domain.ValueObjects.EvaluationId;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IEvaluationRepository
{
    Task<Evaluation?> GetByIdAsync(EvaluationId id, CancellationToken cancellationToken = default);

    Task<LeaderboardEntry?> GetLeaderboardEntryByModelIdAsync(Guid modelId, CancellationToken cancellationToken = default);

    Task<PaginatedList<LeaderboardEntry>> GetLeaderboardAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<PaginatedList<Evaluation>> GetEvaluationHistoryAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<PaginatedList<EvaluationFeedback>> GetFeedbackForModelAsync(Guid modelId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<ArenaSession?> GetArenaSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<Evaluation> CreateEvaluationAsync(Evaluation evaluation, CancellationToken cancellationToken = default);

    Task<ArenaSession> CreateArenaSessionAsync(ArenaSession session, CancellationToken cancellationToken = default);

    Task<LeaderboardEntry> CreateLeaderboardEntryAsync(LeaderboardEntry entry, CancellationToken cancellationToken = default);

    Task<EvaluationFeedback> CreateFeedbackAsync(EvaluationFeedback feedback, CancellationToken cancellationToken = default);

    Task UpdateEvaluationAsync(Evaluation evaluation, CancellationToken cancellationToken = default);

    Task UpdateArenaSessionAsync(ArenaSession session, CancellationToken cancellationToken = default);

    Task UpdateLeaderboardEntryAsync(LeaderboardEntry entry, CancellationToken cancellationToken = default);
}
