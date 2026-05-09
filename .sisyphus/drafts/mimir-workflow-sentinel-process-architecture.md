# Draft: Mimir Workflow Sentinel Process Architecture

## Requirements (confirmed)
- Scope includes `nem.Mimir-typed-ids`, `nem.Workflow`, and `nem.Sentinel`.
- Allowed change depth: architectural refactor.
- Desired target architecture: process-oriented and workflow-first.
- User requirement: when practical and efficient, information flow should be represented as adjustable workflows rather than hardcoded orchestration.
- Preserve critical runtime safety boundaries and active public contracts while refactoring internals.

## Technical Decisions
- Mimir plugin/runtime refactor remains in scope, but it must now align with a larger orchestration redesign across Workflow and Sentinel.
- `nem.Workflow` should become the primary adjustable process/orchestration substrate rather than being used only as a pattern reference.
- `nem.Sentinel` remediation/playbook behavior should be evaluated for migration from imperative code paths toward workflow-driven or policy-driven execution.
- Existing Mimir plugin/tool/service contracts should be preserved and invoked through stable façades from any workflow-driven design.

## Research Findings
- Mimir hardcoded orchestration hotspots:
  - `src/nem.Mimir.Application/Agents/AgentOrchestrator.cs` - central orchestration, tiered escalation, strategy resolution.
  - `src/nem.Mimir.Application/Agents/AgentCoordinator.cs` - sequential/parallel/hierarchical/tiered execution semantics and synthesis.
  - `src/nem.Mimir.Application/Agents/AgentDispatcher.cs` - candidate selection pipeline and fallback heuristics.
  - `src/nem.Mimir.Application/Agents/TierDispatchStrategy.cs`, `TierConfiguration.cs`, `ConfidenceEscalationPolicy.cs` - hardcoded tiering, model mapping, escalation thresholds.
  - Tool invocation flow runs through `src/nem.Mimir.Application/Mcp/McpToolCatalogService.cs` -> `src/nem.Mimir.Infrastructure/Mcp/McpToolAdapter.cs` -> `McpClientManager`.
  - Plugin execution flow runs through `src/nem.Mimir.Application/Common/Interfaces/IPluginService.cs` -> `src/nem.Mimir.Infrastructure/Plugins/PluginManager.cs` with ALC isolation.
- Workflow architecture findings:
  - `src/nem.Workflow.Infrastructure/Sagas/WorkflowOrchestrationSaga.cs` is the current core orchestrator for run progression and next-step dispatch.
  - `src/nem.Workflow.Infrastructure/Handlers/ExecuteStepHandler.cs` contains hardcoded step-type resolution and retry/dispatch behavior.
  - `src/nem.Workflow.Infrastructure/Services/StepExecutorResolver.cs` is the executor lookup seam.
  - `src/nem.Workflow.Infrastructure/Plugins/WorkflowPluginLoader.cs` and `WorkflowPluginCatalog.cs` are existing plugin extension points.
  - `src/nem.Workflow.Infrastructure/Services/FlowDesignerService.cs` and `Executors/MimirPromptStepExecutor.cs` are the main LLM-to-workflow and LLM-in-workflow seams.
- Sentinel architecture findings:
  - `src/nem.Sentinel.Infrastructure/Engine/MapekEngine.cs` is the MAPE-K loop entrypoint.
  - `src/nem.Sentinel.Infrastructure/Plan/PlaybookSelector.cs` and `Execute/RemediationExecutor.cs` are the central planning/execution hotspots.
  - `src/nem.Sentinel.Domain/Playbooks/*` contains imperative remediation logic that is a strong candidate for data-driven/workflow-driven migration.
  - `src/nem.Sentinel.Domain/Autonomy/AutonomyManager.cs` and `L3SafetyGuard.cs` are policy/autonomy control points.
  - Domain events like `RemediationPlannedEvent`, `RemediationExecutedEvent`, and `RemediationOutcomeRecordedEvent` are natural workflow integration hooks.

## Scope Boundaries
- INCLUDE:
  - Mimir plugin/runtime refactor
  - Mimir orchestration externalization into workflow/process definitions
  - Workflow engine/orchestration strategy refactor to support adjustable flows
  - Sentinel playbook/remediation architecture impact where workflow-driven execution is beneficial
  - Policy/autonomy/process guardrails needed for safe cross-repo orchestration
- EXCLUDE:
  - Legacy `nem.Mimir` implementation work
  - Unrelated frontend redesign beyond touched process/orchestration surfaces
  - Breaking public API contracts unless explicitly wrapped/preserved

## Open Questions
- None blocking after user confirmed architectural-refactor depth across all three repos.
