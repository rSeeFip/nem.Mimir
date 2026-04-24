using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Wolverine;
using nem.Contracts.Costs;
using nem.Contracts.TokenOptimization;

namespace nem.Mimir.Application.Tokens;

/// <summary>
/// In-memory implementation of <see cref="ITokenTracker"/> that records token usage events
/// and provides aggregation queries with optional budget enforcement.
/// </summary>
public sealed class TokenTrackerService : ITokenTracker
{
    private const string ServiceName = "nem.mimir";

    private readonly ConcurrentBag<TokenUsageEvent> _events = new();
    private readonly TokenTrackerOptions _options;
    private readonly ILogger<TokenTrackerService> _logger;
    private readonly IMessageBus? _messageBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenTrackerService"/> class.
    /// </summary>
    /// <param name="options">Token tracker configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="messageBus">Optional Wolverine message bus for emitting cost events.</param>
    public TokenTrackerService(TokenTrackerOptions options, ILogger<TokenTrackerService> logger, IMessageBus? messageBus = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _logger = logger;
        _messageBus = messageBus;
    }

    /// <inheritdoc />
    public Task RecordUsageAsync(TokenUsageEvent usageEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usageEvent);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.EnableTracking)
        {
            _logger.LogDebug("Token tracking is disabled; skipping recording for service {ServiceId}.", usageEvent.ServiceId);
            return Task.CompletedTask;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(usageEvent.ServiceId);

        if (usageEvent.InputTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usageEvent), "InputTokens must be non-negative.");
        }

        if (usageEvent.OutputTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usageEvent), "OutputTokens must be non-negative.");
        }

        // Check budget before recording
        if (_options.DefaultBudgetPerService.HasValue)
        {
            var budget = _options.DefaultBudgetPerService.Value;
            var currentCost = ComputeCostForService(usageEvent.ServiceId);
            var projectedCost = currentCost + usageEvent.Cost;

            if (projectedCost > budget)
            {
                _logger.LogWarning(
                    "Budget exceeded for service {ServiceId}: projected cost {ProjectedCost} exceeds budget {Budget}.",
                    usageEvent.ServiceId,
                    projectedCost,
                    budget);
            }
            else if (budget > 0 && (double)(currentCost / budget) >= _options.BudgetWarningThreshold)
            {
                _logger.LogWarning(
                    "Budget warning for service {ServiceId}: current cost {CurrentCost} has reached {Threshold:P0} of budget {Budget}.",
                    usageEvent.ServiceId,
                    currentCost,
                    _options.BudgetWarningThreshold,
                    budget);
            }
        }

        _events.Add(usageEvent);

        _logger.LogDebug(
            "Recorded token usage for service {ServiceId}, model {ModelId}: {InputTokens} in / {OutputTokens} out, cost {Cost}.",
            usageEvent.ServiceId,
            usageEvent.ModelId,
            usageEvent.InputTokens,
            usageEvent.OutputTokens,
            usageEvent.Cost);

        EmitCostEvent(usageEvent);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TokenUsageSummary> GetUsageAsync(string serviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.EnableTracking)
        {
            _logger.LogDebug("Token tracking is disabled; returning empty summary for service {ServiceId}.", serviceId);
            return Task.FromResult(new TokenUsageSummary(serviceId, 0, 0, 0m, 0));
        }

        var filtered = _events
            .Where(e => string.Equals(e.ServiceId, serviceId, StringComparison.Ordinal)
                        && e.Timestamp >= from
                        && e.Timestamp <= to)
            .ToList();

        if (filtered.Count == 0)
        {
            return Task.FromResult(new TokenUsageSummary(serviceId, 0, 0, 0m, 0));
        }

        var summary = new TokenUsageSummary(
            serviceId,
            filtered.Sum(e => (long)e.InputTokens),
            filtered.Sum(e => (long)e.OutputTokens),
            filtered.Sum(e => e.Cost),
            filtered.Count);

        return Task.FromResult(summary);
    }

    /// <inheritdoc />
    public Task<decimal> GetCostAsync(string serviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.EnableTracking)
        {
            _logger.LogDebug("Token tracking is disabled; returning zero cost for service {ServiceId}.", serviceId);
            return Task.FromResult(0m);
        }

        var totalCost = _events
            .Where(e => string.Equals(e.ServiceId, serviceId, StringComparison.Ordinal)
                        && e.Timestamp >= from
                        && e.Timestamp <= to)
            .Sum(e => e.Cost);

        return Task.FromResult(totalCost);
    }

    /// <summary>
    /// Computes the cumulative cost for all recorded events of a given service.
    /// </summary>
    private decimal ComputeCostForService(string serviceId)
    {
        return _events
            .Where(e => string.Equals(e.ServiceId, serviceId, StringComparison.Ordinal))
            .Sum(e => e.Cost);
    }

    private void EmitCostEvent(TokenUsageEvent usageEvent)
    {
        if (_messageBus is null)
        {
            return;
        }

        var now = usageEvent.Timestamp;
        var minuteBucket = now.ToString("yyyyMMddHHmm");
        var totalTokens = usageEvent.InputTokens + usageEvent.OutputTokens;

        var isEmbedding = usageEvent.ModelId.Contains("embed", StringComparison.OrdinalIgnoreCase);
        var resourceType = isEmbedding ? CostResourceType.Embedding : CostResourceType.LlmInference;

        var costEvent = new CostEvent
        {
            IdempotencyKey = $"mimir:{usageEvent.ServiceId}:{minuteBucket}",
            Timestamp = now,
            ServiceId = ServiceName,
            ResourceType = resourceType,
            UsageQuantity = totalTokens,
            UsageUnit = "tokens",
            RawCost = usageEvent.Cost,
            AmortizedCost = usageEvent.Cost,
            Currency = "USD",
            Tags = new Dictionary<string, string>
            {
                ["model_name"] = usageEvent.ModelId,
                ["input_tokens"] = usageEvent.InputTokens.ToString(),
                ["output_tokens"] = usageEvent.OutputTokens.ToString(),
                ["total_tokens"] = totalTokens.ToString(),
            },
        };

        _ = _messageBus.PublishAsync(costEvent)
            .AsTask()
            .ContinueWith(
                t => _logger.LogError(t.Exception, "Failed to emit CostEvent for service {ServiceId}.", usageEvent.ServiceId),
                TaskContinuationOptions.OnlyOnFaulted);
    }
}
