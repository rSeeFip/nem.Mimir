namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using EvaluationId = nem.Mimir.Domain.ValueObjects.EvaluationId;

internal sealed class EvaluationRepository(MimirDbContext context) : IEvaluationRepository
{
    public async Task<Evaluation?> GetByIdAsync(EvaluationId id, CancellationToken cancellationToken = default)
    {
        return await context.Evaluations
            .AsNoTracking()
            .FirstOrDefaultAsync(evaluation => evaluation.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LeaderboardEntry?> GetLeaderboardEntryByModelIdAsync(Guid modelId, CancellationToken cancellationToken = default)
    {
        return await context.LeaderboardEntries
            .FirstOrDefaultAsync(entry => entry.ModelId == modelId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PaginatedList<LeaderboardEntry>> GetLeaderboardAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.LeaderboardEntries
            .AsNoTracking()
            .OrderByDescending(entry => entry.EloScore)
            .ThenBy(entry => entry.ModelId);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<LeaderboardEntry>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

    public async Task<PaginatedList<Evaluation>> GetEvaluationHistoryAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Evaluations
            .AsNoTracking()
            .Where(evaluation => evaluation.UserId == userId)
            .OrderByDescending(evaluation => evaluation.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<Evaluation>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

    public async Task<PaginatedList<EvaluationFeedback>> GetFeedbackForModelAsync(
        Guid modelId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.EvaluationFeedbacks
            .AsNoTracking()
            .Join(
                context.Evaluations.AsNoTracking(),
                feedback => feedback.EvaluationId,
                evaluation => evaluation.Id,
                (feedback, evaluation) => new { feedback, evaluation })
            .Where(joined => joined.evaluation.ModelAId == modelId || joined.evaluation.ModelBId == modelId)
            .OrderByDescending(joined => joined.feedback.CreatedAt)
            .Select(joined => joined.feedback);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<EvaluationFeedback>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

    public async Task<Evaluation> CreateEvaluationAsync(Evaluation evaluation, CancellationToken cancellationToken = default)
    {
        await context.Evaluations.AddAsync(evaluation, cancellationToken).ConfigureAwait(false);
        return evaluation;
    }

    public async Task<LeaderboardEntry> CreateLeaderboardEntryAsync(LeaderboardEntry entry, CancellationToken cancellationToken = default)
    {
        await context.LeaderboardEntries.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task<EvaluationFeedback> CreateFeedbackAsync(EvaluationFeedback feedback, CancellationToken cancellationToken = default)
    {
        await context.EvaluationFeedbacks.AddAsync(feedback, cancellationToken).ConfigureAwait(false);
        return feedback;
    }

    public Task UpdateEvaluationAsync(Evaluation evaluation, CancellationToken cancellationToken = default)
    {
        context.Evaluations.Update(evaluation);
        return Task.CompletedTask;
    }

    public Task UpdateLeaderboardEntryAsync(LeaderboardEntry entry, CancellationToken cancellationToken = default)
    {
        context.LeaderboardEntries.Update(entry);
        return Task.CompletedTask;
    }
}
