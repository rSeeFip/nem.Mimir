#nullable enable

using nem.Contracts.AspNetCore.Organism.MapeK;

namespace nem.Mimir.Infrastructure.Organism.MapeK;

/// <summary>
/// Mimir-specific MAPE-K playbooks for conversational AI health management.
/// Trigger names match anomaly names from <see cref="ConversationalHealthAnalyzer"/>.
/// </summary>
public static class MimirMapeKPlaybooks
{
    public static IReadOnlyList<MapeKPlaybook> Create()
    {
        return
        [
            new MapeKPlaybook(
                Name: "high-response-latency",
                Trigger: "high-response-latency",
                IsSafeAction: true,
                RecommendedActions:
                [
                    "throttle-concurrent-conversations",
                    "switch-to-faster-llm-model",
                    "alert-operator"
                ],
                WorkflowSteps:
                [
                    "measure-p95-response-latency",
                    "identify-slow-llm-providers",
                    "apply-conversation-rate-limiting"
                ]),
            new MapeKPlaybook(
                Name: "high-memory-usage",
                Trigger: "high-memory-usage",
                IsSafeAction: true,
                RecommendedActions:
                [
                    "trim-conversation-context-windows",
                    "evict-stale-sessions",
                    "alert-operator"
                ],
                WorkflowSteps:
                [
                    "measure-memory-pressure",
                    "identify-large-context-windows",
                    "compact-or-archive-old-conversations"
                ]),
            new MapeKPlaybook(
                Name: "session-health-degradation",
                Trigger: "session-health-degradation",
                IsSafeAction: true,
                RecommendedActions:
                [
                    "reset-degraded-sessions",
                    "reduce-max-concurrent-sessions",
                    "alert-operator"
                ],
                WorkflowSteps:
                [
                    "evaluate-session-error-rates",
                    "identify-stuck-or-zombie-sessions",
                    "clean-up-abandoned-sessions"
                ]),
            new MapeKPlaybook(
                Name: "llm-provider-error-spike",
                Trigger: "llm-provider-error-spike",
                IsSafeAction: false,
                RecommendedActions:
                [
                    "failover-to-backup-llm-provider",
                    "enable-response-caching",
                    "alert-operator"
                ],
                WorkflowSteps:
                [
                    "check-llm-provider-status",
                    "route-traffic-to-healthy-providers",
                    "queue-requests-for-retry"
                ])
        ];
    }
}
