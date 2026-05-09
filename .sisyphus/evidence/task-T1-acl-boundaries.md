# T1 ACL Boundaries

## Rule of record

The three repos must not add direct domain-model project references to each other. Cross-repo exchange must go through:

- `nem.Contracts`
- explicit adapter/translator seams
- serialized workflow definitions
- domain events published at repo boundaries

## Repo exposure map

### nem.Mimir-typed-ids

**Stable outward seams**

- `IPluginService` — plugin lifecycle/execution façade
- `IToolProvider` — tool catalog/invocation façade
- `ILlmService` — LLM send/stream/model-list façade

**What stays internal**

- All Mimir-local IDs (`ConversationId`, `MessageId`, `UserId`, etc.)
- Mimir plugin/domain implementation details (`PluginManager`, internal persistence entities)
- Adapter-specific orchestration state

**ACL policy**

- Workflow may orchestrate Mimir only through tool/plugin/LLM façades or serialized workflow step adapters.
- Sentinel may observe/coordinate with Mimir only through shared contract IDs, ecosystem events, or dedicated anti-corruption adapters.
- No repo may consume Mimir local IDs directly across boundaries.

### nem.Workflow

**Stable outward seams**

- `NemFlowDefinition`
- `WorkflowStep`
- `StepDefinition` polymorphic hierarchy
- Shared workflow IDs from `nem.Contracts.Identity`

**What stays internal**

- Wolverine saga orchestration internals
- executor resolution/runtime dispatch details
- persistence projections/documents

**ACL policy**

- Mimir may supply or consume workflow definitions only through `NemFlowDefinition` and shared workflow IDs.
- Sentinel may route remediation through Workflow only via flow-definition/execution adapters, never by reaching into Workflow infrastructure classes.
- Workflow must not depend directly on Mimir or Sentinel domain models.

### nem.Sentinel

**Stable outward seams**

- `RemediationPlan`
- `RemediationStep`
- `RemediationPlannedEvent`
- `RemediationExecutedEvent`
- `RemediationOutcomeRecordedEvent`

**What stays internal**

- `MapekEngine`, autonomy implementation details, cooldown storage, playbook internals
- in-process execution logic and persistence projections
- Sentinel-local IDs as direct foreign keys in other repos

**ACL policy**

- Workflow may execute Sentinel remediation only through a Sentinel-owned adapter that translates `RemediationPlan`/events into workflow-native execution steps.
- Mimir may react to Sentinel outcomes only through events/shared IDs, never through Sentinel runtime services.
- No repo may bypass `AutonomyManager`, `L3SafetyGuard`, cooldown, or approval semantics.

## Allowed dependency directions

| Consumer | Provider | Allowed surface | Forbidden surface |
|---|---|---|---|
| Mimir | Workflow | `NemFlowDefinition`, shared workflow IDs, dedicated adapters | Workflow sagas, repositories, executors |
| Mimir | Sentinel | remediation events, shared contract IDs, dedicated adapters | `MapekEngine`, playbooks, executor internals |
| Workflow | Mimir | `IPluginService`, `IToolProvider`, `ILlmService`, adapter DTOs | Mimir persistence/domain internals |
| Workflow | Sentinel | remediation events/DTOs through adapter layer | Sentinel autonomy or execution internals |
| Sentinel | Mimir | shared skill/correlation IDs, Mimir-published events, adapter DTOs | Mimir local ID types and runtime internals |
| Sentinel | Workflow | `NemFlowDefinition`, shared workflow IDs, execution adapter contracts | Workflow infrastructure internals |

## Adapter responsibilities

1. Normalize IDs at the boundary.
   - Mimir local IDs must be translated before leaving the repo.
   - Workflow shared IDs can cross as-is.
   - Sentinel local remediation IDs must remain Sentinel-owned even when projected into workflow state.

2. Keep event ownership with the publishing repo.
   - Sentinel remediation events remain Sentinel-native audit records.
   - Workflow run/step events remain Workflow-native execution records.
   - Mimir tool/plugin/LLM activity remains behind Mimir façades.

3. For future integration, prefer anti-corruption translators over shared domain classes.

## Baseline enforcement targets

- Contract pin tests must fail on signature drift.
- Typed IDs must remain value types with `Guid` backing.
- Cross-repo work must add adapters before adding any new direct project reference.
