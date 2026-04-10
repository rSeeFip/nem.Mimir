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

## T4 workflow seam learnings
- `nem.Workflow` project files disable default compile item discovery, so every new interface/service/test file must be explicitly added to the relevant `.csproj` or the build will fail with missing-type errors even when source files exist.
- Preserving workflow runtime behavior was cleanest by moving the existing first-step and next-step resolution algorithm intact into `DefaultOrchestrationStrategy`, then injecting `IOrchestrationStrategy` into saga start/step-complete/step-fail/resume paths.
- `StepExecutorResolver` is a better seam when it accepts `StepDefinition` directly and delegates discriminator mapping to `IStepTypeRegistry`; this removes duplicated step-type switch logic from `ExecuteStepHandler` while keeping keyed DI resolution unchanged.
- Current default parity intentionally preserves existing unsupported runtime behavior for `IdentityStepDefinition` and `BranchAllStepDefinition` in `ExecuteStepHandler`/resolver flow; the new default registry still throws for those definitions because the old handler switch also rejected them.
- `dotnet build nem.Workflow.slnx` now passes, while analyzer noise remains baseline-only; CA2000 warnings are pre-existing in infrastructure test files such as `Triggers/Content/GitHubPollingTriggerProviderTests.cs`, `Executors/Content/RedditFetchExecutorTests.cs`, `RssFeedPollingTriggerProviderTests.cs`, `GitHubFetchExecutorTests.cs`, `WebFetchExecutorTests.cs`, `WebSearchExecutorTests.cs`, `YouTubeFetchExecutorTests.cs`, and `Plugins/WorkflowPluginLoaderTests.cs`.

## T5 Sentinel seam learnings
- `AutonomyManager` is `internal sealed` in `nem.Sentinel.Domain`. To write adapter tests in `nem.Sentinel.Infrastructure.Tests`, add `InternalsVisibleTo` for `nem.Sentinel.Infrastructure.Tests` in `nem.Sentinel.Domain.csproj`. Already granted to `nem.Sentinel.Infrastructure` (production) and `nem.Sentinel.Domain.Tests`.
- The pre-existing duplicate `IPlaybook` type (defined in both `nem.Sentinel.Domain/Playbooks/_Shared/IPlaybook.cs` and `nem.Sentinel.Application/Interfaces/IPlaybook.cs`, both in namespace `nem.Sentinel.Application.Interfaces`) causes LSP "type exists in both assemblies" noise but the dotnet compiler resolves correctly. Avoid using `IPlaybook` directly in `Application.Tests` — the project's transitive closure sees both assemblies and NSubstitute auto-creates an unexpected substitute instead of returning null. Tests in `Infrastructure.Tests` work fine since they reference Infrastructure which resolves cleanly.
- `AutonomyManagerGate.ContainsForbiddenActions` uses `Enum.GetNames<ForbiddenActionCategory>()` for case-insensitive string matching — this means any step `ActionType` that case-insensitively matches an enum name (e.g. `"deletedata"`) is blocked. Tests should cover both exact and lowercase variations.
- `IPlaybookLoader` registered as Scoped (matching playbook `AddScoped` registrations); `IAutonomyGate` registered as Singleton (matching `AutonomyManager` singleton lifecycle).
- 4 pre-existing failures in `nem.Sentinel.Infrastructure.Tests`: `ChaosMiddlewareTests.BeforeAsync_injects_latency_when_policy_matches`, `MapekEngineTests.Runs_full_cycle_when_analysis_has_playbooks`, and two `WolverineMonitorIntegrationTests` (RabbitMQ container timeout). These are unrelated to T5 and were failing before changes.

## T5 scope-creep removal learnings
- T5 verification caught `SentinelInitManifestProvider.cs` (Api/Initialization), its test file (`SentinelInitManifestProviderTests.cs`), and two Program.cs lines (`AddNemInitialization<SentinelInitManifestProvider>()` + `MapNemInitialization()`) that were added during implementation but are unrelated to the seam task. These were committed alongside T5 changes in the same `git add -A` sweep. Fix: `git rm` the files, revert the Program.cs usings and call-sites, commit separately as `[T5] Narrow Sentinel seams to task scope`.
- Lesson: Always use `git add -p` or explicit file paths rather than `git add -A` when making focused seam changes. The `Api/Initialization/` directory is a separate concern (init manifest / MCP init protocol) that has its own task boundary and must not be bundled with infrastructure seam work.
- After removal: `dotnet build nem.Sentinel.slnx` → Build succeeded (2 NU1903 baseline warnings, 0 errors). `dotnet test Application.Tests` → 9/9 pass. `dotnet test Infrastructure.Tests --filter seam targets` → 54/54 pass.

## T6 runtime/catalog learnings
- Built-in plugins behave deterministically when the runtime keeps an explicit registration order and lists/unloads from that order instead of iterating a concurrent dictionary.
- A separate runtime catalog seam (`IPluginRuntimeCatalog`) lets DI-hosted built-ins share the same lifecycle path as external plugins without exposing `PluginManager` through `IPluginService` casts.
- Marketplace built-ins should tolerate missing remote configuration during startup and fail lazily at execution time so the host can boot without optional skills infrastructure.
