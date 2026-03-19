namespace nem.Mimir.Infrastructure.Lifecycle;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using nem.Contracts.Lifecycle;
using Wolverine.Attributes;

/// <summary>
/// Wolverine handler for <see cref="RetentionPolicyChanged"/> events in Mimir.
/// 
/// Simpler than the MCP template handler — Mimir is a downstream consumer.
/// Updates the local <see cref="MimirRetentionPolicyCache"/> with the new policy values.
/// Follows the HomeAssistant pattern: lightweight, static, no tier transition triggering.
/// </summary>
public static class MimirRetentionPolicyChangedHandler
{
    private const string ServiceName = "Mimir";
    private static readonly ActivitySource ActivitySource = new("nem.Mimir.Infrastructure.Lifecycle.MimirRetentionPolicyChangedHandler");

    /// <summary>
    /// Handles incoming <see cref="RetentionPolicyChanged"/> events by updating
    /// the local retention policy cache.
    /// </summary>
    [WolverineHandler]
    public static async Task HandleAsync(
        RetentionPolicyChanged message,
        MimirRetentionPolicyCache cache,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("nem.Mimir.Infrastructure.Lifecycle.MimirRetentionPolicyChangedHandler");

        using var activity = ActivitySource.StartActivity("retention_policy.handle", ActivityKind.Consumer);
        activity?.SetTag("lifecycle.policy_id", message.RetentionPolicyId.ToString());
        activity?.SetTag("lifecycle.policy_name", message.PolicyName);
        activity?.SetTag("lifecycle.is_active", message.IsActive);
        activity?.SetTag("lifecycle.retention_days", message.RetentionDays);
        activity?.SetTag("service.name", ServiceName);

        try
        {
            cache.UpdatePolicy(message);

            logger.LogInformation(
                "Updated Mimir retention policy cache for {PolicyName} (Id: {PolicyId}, Active: {IsActive}, RetentionDays: {RetentionDays})",
                message.PolicyName,
                message.RetentionPolicyId,
                message.IsActive,
                message.RetentionDays);

            activity?.SetTag("lifecycle.cache_updated", true);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to process retention policy change for {PolicyName} (Id: {PolicyId}) in Mimir",
                message.PolicyName,
                message.RetentionPolicyId);

            activity?.SetTag("lifecycle.handler_error", ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Do NOT rethrow — cache update failure should not break message processing.
            // The background revalidation will eventually correct stale cache entries.
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
