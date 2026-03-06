using Microsoft.Extensions.Logging;
using nem.Contracts.Memory;

namespace Mimir.Application.Agents;

public sealed class AgentMemoryContextService : IAgentMemoryContextService
{
    private readonly IWorkingMemory _workingMemory;
    private readonly IEpisodicMemory _episodicMemory;
    private readonly ISemanticMemory _semanticMemory;
    private readonly AgentMemoryContextOptions _options;
    private readonly ILogger<AgentMemoryContextService> _logger;

    public AgentMemoryContextService(
        IWorkingMemory workingMemory,
        IEpisodicMemory episodicMemory,
        ISemanticMemory semanticMemory,
        AgentMemoryContextOptions options,
        ILogger<AgentMemoryContextService> logger)
    {
        _workingMemory = workingMemory ?? throw new ArgumentNullException(nameof(workingMemory));
        _episodicMemory = episodicMemory ?? throw new ArgumentNullException(nameof(episodicMemory));
        _semanticMemory = semanticMemory ?? throw new ArgumentNullException(nameof(semanticMemory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MemoryContext> AssembleContextAsync(
        string conversationId,
        string userId,
        string agentType,
        int maxTokenBudget,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var safeTokenBudget = Math.Max(0, maxTokenBudget);

        var semanticBudget = CalculateBudget(safeTokenBudget, _options.SemanticBudgetPercent);
        var episodicBudget = CalculateBudget(safeTokenBudget, _options.EpisodicBudgetPercent);
        var workingBudget = CalculateBudget(safeTokenBudget, _options.WorkingBudgetPercent);

        var profile = ResolveAccessProfile(agentType);

        var semanticTask = profile.AllowSemantic
            ? QuerySemanticAsync(userId, semanticBudget, profile.SemanticAccessMultiplier, ct)
            : Task.FromResult<IReadOnlyList<string>>([]);

        var episodicTask = profile.AllowEpisodic
            ? QueryEpisodicAsync(userId, episodicBudget, profile.EpisodicAccessMultiplier, ct)
            : Task.FromResult<IReadOnlyList<string>>([]);

        var workingTask = profile.AllowWorking
            ? QueryWorkingAsync(conversationId, workingBudget, profile.WorkingAccessMultiplier, ct)
            : Task.FromResult<IReadOnlyList<string>>([]);

        await Task.WhenAll(semanticTask, episodicTask, workingTask).ConfigureAwait(false);

        var semanticFacts = semanticTask.Result;
        var episodicSummaries = episodicTask.Result;
        var workingMessages = workingTask.Result;

        var totalTokenEstimate = EstimateTokens(semanticFacts)
            + EstimateTokens(episodicSummaries)
            + EstimateTokens(workingMessages);

        return new MemoryContext(semanticFacts, episodicSummaries, workingMessages, totalTokenEstimate);
    }

    private async Task<IReadOnlyList<string>> QuerySemanticAsync(
        string userId,
        int budgetTokens,
        float multiplier,
        CancellationToken ct)
    {
        var adjustedBudget = ApplyMultiplier(budgetTokens, multiplier);
        if (adjustedBudget <= 0)
        {
            return [];
        }

        var estimatedCount = Math.Max(1, adjustedBudget / 64);
        var query = $"user:{userId}";

        var facts = await ExecuteWithTimeoutAsync(
            () => _semanticMemory.QueryAsync(query, estimatedCount, ct),
            "semantic",
            ct).ConfigureAwait(false);

        return LimitToBudget(
            facts.Select(f => $"{f.Subject} {f.Predicate} {f.Object} (confidence: {f.Confidence:0.##})"),
            adjustedBudget);
    }

    private async Task<IReadOnlyList<string>> QueryEpisodicAsync(
        string userId,
        int budgetTokens,
        float multiplier,
        CancellationToken ct)
    {
        var adjustedBudget = ApplyMultiplier(budgetTokens, multiplier);
        if (adjustedBudget <= 0)
        {
            return [];
        }

        var estimatedCount = Math.Max(1, adjustedBudget / 96);
        var episodes = await ExecuteWithTimeoutAsync(
            () => _episodicMemory.SearchSimilarAsync(userId, estimatedCount, ct),
            "episodic",
            ct).ConfigureAwait(false);

        return LimitToBudget(
            episodes.Select(e => e.Summary),
            adjustedBudget);
    }

    private async Task<IReadOnlyList<string>> QueryWorkingAsync(
        string conversationId,
        int budgetTokens,
        float multiplier,
        CancellationToken ct)
    {
        var adjustedBudget = ApplyMultiplier(budgetTokens, multiplier);
        if (adjustedBudget <= 0)
        {
            return [];
        }

        var estimatedCount = Math.Max(1, adjustedBudget / 48);
        var messages = await ExecuteWithTimeoutAsync(
            () => _workingMemory.GetRecentAsync(conversationId, estimatedCount, ct),
            "working",
            ct).ConfigureAwait(false);

        return LimitToBudget(
            messages.Select(m => $"{m.Role}: {m.Content}"),
            adjustedBudget);
    }

    private async Task<IReadOnlyList<T>> ExecuteWithTimeoutAsync<T>(
        Func<Task<IReadOnlyList<T>>> query,
        string storeName,
        CancellationToken ct)
    {
        var timeout = TimeSpan.FromMilliseconds(Math.Max(1, _options.MemoryRetrievalTimeoutMs));
        var queryTask = query();
        var timeoutTask = Task.Delay(timeout, ct);

        var completed = await Task.WhenAny(queryTask, timeoutTask).ConfigureAwait(false);
        if (completed == queryTask)
        {
            try
            {
                return await queryTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Store} memory retrieval failed; falling back to empty results.", storeName);
                return [];
            }
        }

        _logger.LogWarning("{Store} memory retrieval timed out after {TimeoutMs}ms; using empty results.", storeName, _options.MemoryRetrievalTimeoutMs);
        return [];
    }

    private IReadOnlyList<string> LimitToBudget(IEnumerable<string> items, int budgetTokens)
    {
        if (budgetTokens <= 0)
        {
            return [];
        }

        var selected = new List<string>();
        var runningTokens = 0;

        foreach (var item in items)
        {
            var estimate = EstimateTokens(item);
            if (estimate <= 0)
            {
                continue;
            }

            if (runningTokens + estimate > budgetTokens)
            {
                break;
            }

            selected.Add(item);
            runningTokens += estimate;
        }

        return selected;
    }

    private int EstimateTokens(IEnumerable<string> items)
    {
        return items.Sum(EstimateTokens);
    }

    private int EstimateTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var perChar = _options.EstimatedTokensPerChar <= 0
            ? 0.25f
            : _options.EstimatedTokensPerChar;

        return Math.Max(1, (int)Math.Ceiling(value.Length * perChar));
    }

    private static int CalculateBudget(int maxTokenBudget, int percentage)
    {
        var bounded = Math.Clamp(percentage, 0, 100);
        return (int)Math.Floor(maxTokenBudget * (bounded / 100d));
    }

    private static int ApplyMultiplier(int value, float multiplier)
    {
        var bounded = Math.Clamp(multiplier, 0f, 1f);
        return (int)Math.Floor(value * bounded);
    }

    private static AccessProfile ResolveAccessProfile(string agentType)
    {
        var normalized = agentType?.Trim().ToLowerInvariant() ?? string.Empty;

        return normalized switch
        {
            "explore" => new AccessProfile(
                AllowSemantic: true,
                AllowEpisodic: true,
                AllowWorking: false,
                SemanticAccessMultiplier: 1.0f,
                EpisodicAccessMultiplier: 0.5f,
                WorkingAccessMultiplier: 0f),
            "general" => new AccessProfile(
                AllowSemantic: true,
                AllowEpisodic: true,
                AllowWorking: true,
                SemanticAccessMultiplier: 0.5f,
                EpisodicAccessMultiplier: 1.0f,
                WorkingAccessMultiplier: 1.0f),
            "execute" => new AccessProfile(
                AllowSemantic: false,
                AllowEpisodic: false,
                AllowWorking: true,
                SemanticAccessMultiplier: 0f,
                EpisodicAccessMultiplier: 0f,
                WorkingAccessMultiplier: 1.0f),
            _ => new AccessProfile(
                AllowSemantic: true,
                AllowEpisodic: true,
                AllowWorking: true,
                SemanticAccessMultiplier: 1.0f,
                EpisodicAccessMultiplier: 1.0f,
                WorkingAccessMultiplier: 1.0f),
        };
    }

    private sealed record AccessProfile(
        bool AllowSemantic,
        bool AllowEpisodic,
        bool AllowWorking,
        float SemanticAccessMultiplier,
        float EpisodicAccessMultiplier,
        float WorkingAccessMultiplier);
}
