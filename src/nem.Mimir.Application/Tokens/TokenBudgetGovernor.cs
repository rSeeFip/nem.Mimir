using Microsoft.Extensions.Options;
using nem.Contracts.TokenOptimization;

namespace nem.Mimir.Application.Tokens;

/// <summary>
/// Evaluates token budgets using tracker summaries and configured thresholds.
/// </summary>
public sealed class TokenBudgetGovernor : ITokenBudgetPolicy
{
    private readonly ITokenTracker _tokenTracker;
    private readonly TokenBudgetGovernorOptions _options;

    public TokenBudgetGovernor(ITokenTracker tokenTracker, IOptions<TokenBudgetGovernorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(tokenTracker);
        ArgumentNullException.ThrowIfNull(options);

        _tokenTracker = tokenTracker;
        _options = options.Value ?? throw new ArgumentException("Token budget governor options are required.", nameof(options));
    }

    public async Task<BudgetCheckResult> CheckBudgetAsync(TokenBudgetContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ServiceId);

        if (context.InputTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "InputTokens must be non-negative.");
        }

        if (context.OutputTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "OutputTokens must be non-negative.");
        }

        var budgetScopeId = context.BudgetScopeId;
        var budgetLimit = ResolveBudgetLimit(budgetScopeId);
        var currentUsage = await _tokenTracker.GetUsageAsync(budgetScopeId, DateTimeOffset.MinValue, context.Timestamp, cancellationToken);

        var currentTokens = currentUsage.TotalInputTokens + currentUsage.TotalOutputTokens;
        var requestedTokens = (long)context.InputTokens + context.OutputTokens;
        var projectedTokens = currentTokens + requestedTokens;

        if (projectedTokens > budgetLimit)
        {
            return new BudgetCheckResult(
                BudgetAction.Deny,
                projectedTokens,
                budgetLimit,
                $"Token budget exceeded for tenant '{budgetScopeId}'.");
        }

        var warningThreshold = CalculateWarningThreshold(budgetLimit);
        if (projectedTokens >= warningThreshold)
        {
            return new BudgetCheckResult(
                BudgetAction.Warn,
                projectedTokens,
                budgetLimit,
                $"Token budget warning threshold reached for tenant '{budgetScopeId}'.");
        }

        return new BudgetCheckResult(BudgetAction.Allow, projectedTokens, budgetLimit, null);
    }

    public Task RecordUsageAsync(TokenUsage usage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usage);

        return _tokenTracker.RecordUsageAsync(
            new TokenUsageEvent(
                usage.ServiceId,
                usage.ModelId,
                usage.InputTokens,
                usage.OutputTokens,
                usage.Cost,
                usage.Timestamp),
            cancellationToken);
    }

    private long ResolveBudgetLimit(string tenantId)
    {
        if (_options.TenantOverrides.TryGetValue(tenantId, out var overrideBudget))
        {
            return overrideBudget;
        }

        return _options.DefaultBudget;
    }

    private decimal CalculateWarningThreshold(long budgetLimit)
    {
        return budgetLimit * (decimal)(_options.WarnThresholdPercent / 100d);
    }
}
