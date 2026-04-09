# Learnings

## Conventions
- .slnx format (canonical). `dotnet build nem.X.slnx`, `dotnet test nem.X.slnx`
- Strongly-typed IDs only (nem.Contracts). No raw Guid/string
- MediatR in Mimir (NOT Wolverine)
- File-scoped namespaces, sealed classes, nullable, TreatWarningsAsErrors
- Mapperly (source-generated mapping), FluentValidation, Marten for persistence
- ALC isolation for plugins — NEVER crash host on plugin failure
- xUnit v3 + NSubstitute for testing
- Evidence saved to `.sisyphus/evidence/task-N-*.txt`

## Pre-existing LSP errors in mimir-chat (NOT our problem - pre-existing)
- ChatMessage.tsx, Chat.tsx, ChatInput.tsx: various React hook/type errors
- globals.css: tailwind at-rule warnings
- These are baseline pre-existing issues, do not count against us

## Repos in scope
- /workspace/wmreflect/nem.Mimir-typed-ids (solution: nem.Mimir.slnx)
- /workspace/wmreflect/nem.Workflow (solution: nem.Workflow.slnx)
- /workspace/wmreflect/nem.Sentinel (solution: nem.Sentinel.slnx)

## Key anti-patterns to avoid
- No `Activator.CreateInstance` for built-in plugins
- No `IPluginService as PluginManager` cast
- No `GetAwaiter().GetResult()` in startup
- No cross-plugin shared static state
- No bypassing AutonomyManager/L3SafetyGuard/cooldown/approval
- No unvalidated LLM-generated flows running unsafe node types
- No TODOs/FIXMEs/HACKs left in touched production files

## T1 contract baseline learnings
- The three repos currently have **no direct source-level references to each other**; the only shared compile-time boundary is `nem.Contracts`, plus serialized/event surfaces.
- `nem.Mimir-typed-ids` still owns a separate local `ITypedId<Guid>` abstraction and 14 local Guid-backed IDs, so future cross-repo integrations need explicit ID normalization adapters instead of leaking Mimir-local IDs.
- `nem.Workflow` is already standardized on shared `nem.Contracts.Identity` workflow IDs (`WorkflowId`, `WorkflowRunId`, `WorkflowStepId`, `WorkflowTriggerId`, `WorkflowVersionId`), making it the cleanest baseline contract surface.
- `nem.Sentinel` mixes local remediation/incident/playbook IDs with shared ecosystem IDs (`CorrelationId`, `SkillId`, `SkillSubmissionId`, organism IDs), so ACLs must keep Sentinel-local IDs repo-owned while allowing shared contracts to cross boundaries.
- `nem.Workflow.Domain.Tests` disables default compile item discovery, so any new contract-pin test file must be explicitly added to the test project file.

## T2 plugin foundation learnings
- Mimir plugin ALC sharing must mirror SDK behavior: `nem.Contracts`, `nem.Plugins.Sdk`, and `System*` stay in the default host ALC; Mimir/private plugin assemblies stay isolated in the collectible plugin ALC.
- Built-in plugins should be resolved as DI-managed `IPlugin` instances and registered through the concrete `PluginManager`, never through `IPluginService` downcasts or dynamic built-in activation paths.
- Plugin lifecycle operations in Mimir need defensive `try/catch` around load, registration initialization, shutdown, and ALC unload so plugin faults are logged without crashing the host.
- Current `dotnet build nem.Mimir.slnx` and broad `dotnet test ... --filter Category!=Integration` are blocked by pre-existing repo issues outside T2 scope: broken `nem.Mimir.Signal.Tests`, broad E2E/API integration failures, and existing solution-structure expectations in `nem.Mimir.Domain.Tests`.

## T3 orchestration seam learnings
- `IOrchestrationPlan` can stay minimal if it only exposes the data/decisions consumed by `AgentOrchestrator`, `TierDispatchStrategy`, and `ConfidenceEscalationPolicy`: default max turns, strategy resolution, entry tier, agent tier mapping, model mapping, escalation threshold, and next-tier progression.
- Preserving orchestration behavior verbatim is easiest when the new default provider owns the prior constants/heuristics directly instead of splitting them across mutable configuration plus fallback branches.
- `AgentExecutionContext` needed a `SetMaxTurns(int)` seam so the provider can be resolved before default max-turn fallback is applied without changing turn-sharing behavior for child contexts.
- The new required `IOrchestrationPlanProvider` constructor dependency ripples into integration tests that new up `AgentOrchestrator` directly; update those call sites when introducing the seam.
- `BackgroundTaskExecutorTests.ProcessTaskAsync_ShouldCompleteAndCacheResult` is timing-sensitive around `IProgress<T>` callbacks; waiting specifically for the `Completed` event is more stable than waiting on a raw event count.
