namespace nem.Mimir.Infrastructure.Tokens;

using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;

internal sealed class BudgetLlmServiceDecorator(
    ILlmService inner,
    ITokenBudgetPolicy policy,
    ICurrentUserService? currentUserService = null,
    IHttpContextAccessor? httpContextAccessor = null,
    ILogger<BudgetLlmServiceDecorator>? logger = null) : ILlmService
{
    private const string ServiceId = "nem.mimir";
    private const string BudgetDeniedFinishReason = "budget_denied";

    private readonly ILogger<BudgetLlmServiceDecorator> _logger = logger ?? NullLogger<BudgetLlmServiceDecorator>.Instance;

    public async Task<LlmResponse> SendMessageAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var budgetScopeId = ResolveBudgetScopeId();
        var budgetCheck = await CheckBudgetAsync(model, messages, cancellationToken).ConfigureAwait(false);

        if (budgetCheck.Action == BudgetAction.Deny)
        {
            return CreateDeniedResponse(model, budgetCheck.Reason);
        }

        if (budgetCheck.Action == BudgetAction.Warn)
        {
            LogBudgetWarning(model, budgetCheck);
        }

        var response = await inner.SendMessageAsync(model, messages, cancellationToken).ConfigureAwait(false);

        await policy.RecordUsageAsync(
            new TokenUsage(
                budgetScopeId,
                ResolveModelId(response.Model, model),
                response.PromptTokens,
                response.CompletionTokens,
                0m,
                DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        return response;
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamMessageAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var budgetCheck = await CheckBudgetAsync(model, messages, cancellationToken).ConfigureAwait(false);

        if (budgetCheck.Action == BudgetAction.Deny)
        {
            yield return new LlmStreamChunk(
                Content: budgetCheck.Reason ?? "Token budget denied.",
                Model: ResolveModelId(model),
                FinishReason: BudgetDeniedFinishReason);
            yield break;
        }

        if (budgetCheck.Action == BudgetAction.Warn)
        {
            LogBudgetWarning(model, budgetCheck);
        }

        await foreach (var chunk in inner.StreamMessageAsync(model, messages, cancellationToken).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    public Task<IReadOnlyList<LlmModelInfoDto>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        => inner.GetAvailableModelsAsync(cancellationToken);

    private Task<BudgetCheckResult> CheckBudgetAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken)
    {
        var budgetScopeId = ResolveBudgetScopeId();

        return policy.CheckBudgetAsync(
            new TokenBudgetContext(
                ServiceId,
                ResolveModelId(model),
                EstimateInputTokens(messages),
                0,
                0m,
                DateTimeOffset.UtcNow,
                budgetScopeId),
            cancellationToken);
    }

    private void LogBudgetWarning(string model, BudgetCheckResult budgetCheck)
    {
        _logger.LogWarning(
            "Token budget warning for service {ServiceId} and model {ModelId}. Projected usage {ProjectedUsage} of {BudgetLimit}. Reason: {Reason}",
            ResolveBudgetScopeId(),
            ResolveModelId(model),
            budgetCheck.ProjectedSpend,
            budgetCheck.BudgetLimit,
            budgetCheck.Reason ?? "Warning threshold reached.");
    }

    private string ResolveBudgetScopeId()
    {
        var principal = httpContextAccessor?.HttpContext?.User;
        var tenantClaim = currentUserService?.TenantId
            ?? principal?.FindFirst("tenant_id")?.Value
            ?? principal?.FindFirst("tenantId")?.Value
            ?? principal?.FindFirst("workspace_id")?.Value
            ?? principal?.FindFirst("workspaceId")?.Value
            ?? principal?.FindFirst("organization_id")?.Value
            ?? principal?.FindFirst("organizationId")?.Value;

        if (!string.IsNullOrWhiteSpace(tenantClaim))
        {
            return tenantClaim;
        }

        var userId = currentUserService?.UserId
            ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrWhiteSpace(userId)
            ? ServiceId
            : userId;
    }

    private static LlmResponse CreateDeniedResponse(string model, string? reason)
    {
        return new LlmResponse(
            Content: reason ?? "Token budget denied.",
            Model: ResolveModelId(model),
            PromptTokens: 0,
            CompletionTokens: 0,
            TotalTokens: 0,
            FinishReason: BudgetDeniedFinishReason);
    }

    private static string ResolveModelId(string? primaryModel, string? fallbackModel = null)
    {
        if (!string.IsNullOrWhiteSpace(primaryModel))
        {
            return primaryModel;
        }

        return fallbackModel ?? string.Empty;
    }

    private static int EstimateInputTokens(IReadOnlyList<LlmMessage> messages)
    {
        return messages.Sum(message => EstimateTokens(message.Role) + EstimateTokens(message.Content));
    }

    private static int EstimateTokens(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(content.Length / 4d));
    }
}
