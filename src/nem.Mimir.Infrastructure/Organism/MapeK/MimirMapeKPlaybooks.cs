#nullable enable

using nem.Contracts.AspNetCore.Organism.MapeK;

namespace nem.Mimir.Infrastructure.Organism.MapeK;

public static class MimirMapeKPlaybooks
{
    public static IReadOnlyList<MapeKPlaybook> Create()
    {
        return
        [
            new MapeKPlaybook(
                Name: "conversation-routing-failover",
                Trigger: "conversation-routing-failover",
                IsSafeAction: true,
                RecommendedActions:
                [
                    "reroute-conversation-channel",
                    "enable-circuit-breaker",
                    "alert-operator"
                ],
                WorkflowSteps:
                [
                    "inspect-channel-adapter-health",
                    "switch-to-healthy-adapter",
                    "emit-routing-diagnostics"
                ]),
            new MapeKPlaybook(
                Name: "model-selection-degradation",
                Trigger: "model-selection-degradation",
                IsSafeAction: false,
                RecommendedActions:
                [
                    "fallback-to-stable-model-profile",
                    "tighten-selection-policy",
                    "alert-operator"
                ],
                WorkflowSteps:
                [
                    "evaluate-model-scorecard",
                    "apply-model-fallback",
                    "notify-evaluation-pipeline"
                ]),
            new MapeKPlaybook(
                Name: "inference-latency-spike",
                Trigger: "inference-latency-spike",
                IsSafeAction: true,
                RecommendedActions:
                [
                    "reduce-parallel-operations",
                    "reduce-cache-ttl",
                    "recommend-scale-out"
                ],
                WorkflowSteps:
                [
                    "compute-latency-p95",
                    "shed-noncritical-load",
                    "request-capacity-increase"
                ]),
            new MapeKPlaybook(
                Name: "semantic-cache-inefficiency",
                Trigger: "semantic-cache-inefficiency",
                IsSafeAction: true,
                RecommendedActions:
                [
                    "reduce-cache-ttl",
                    "trigger-gc",
                    "refresh-hot-cache-keys"
                ],
                WorkflowSteps:
                [
                    "audit-cache-hit-ratio",
                    "flush-stale-cache-segments",
                    "warm-critical-conversation-context"
                ]),
            new MapeKPlaybook(
                Name: "tool-call-timeout-burst",
                Trigger: "tool-call-timeout-burst",
                IsSafeAction: false,
                RecommendedActions:
                [
                    "degrade-tool-usage-policy",
                    "reroute-to-alt-provider",
                    "alert-operator"
                ],
                WorkflowSteps:
                [
                    "identify-timing-out-tools",
                    "apply-tool-timeout-guardrails",
                    "escalate-to-oncall"
                ])
        ];
    }
}
