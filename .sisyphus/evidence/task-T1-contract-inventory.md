# T1 Contract Inventory

## Scope

This inventory freezes the cross-repo contract baseline for:

- `/workspace/wmreflect/nem.Mimir-typed-ids`
- `/workspace/wmreflect/nem.Workflow`
- `/workspace/wmreflect/nem.Sentinel`

## Cross-repo dependency scan

- No direct source-level references from Mimir to Workflow or Sentinel were found.
- No direct source-level references from Workflow to Mimir or Sentinel were found.
- No direct source-level references from Sentinel to Mimir or Workflow were found.
- Current shared contract coupling runs through `nem.Contracts` plus serialized/event boundaries.

## Contract surfaces

| Repo | Contract | Kind | Location | Dependency edge |
|---|---|---|---|---|
| nem.Mimir-typed-ids | `IPluginService` | interface | `src/nem.Mimir.Application/Common/Interfaces/IPluginService.cs` | Application façade over `nem.Mimir.Domain.Plugins`; no direct Workflow/Sentinel dependency |
| nem.Mimir-typed-ids | `IToolProvider` | interface | `src/nem.Mimir.Domain/Tools/IToolProvider.cs` | Domain tool abstraction; adapter-safe seam for future Workflow orchestration |
| nem.Mimir-typed-ids | `ILlmService` | interface | `src/nem.Mimir.Application/Common/Interfaces/ILlmService.cs` | Application façade over provider-agnostic LLM DTOs; no direct Workflow/Sentinel dependency |
| nem.Workflow | `NemFlowDefinition` | record class | `src/nem.Workflow.Domain/Models/NemFlowDefinition.cs` | Depends on `WorkflowId` from `nem.Contracts.Identity`; canonical workflow definition boundary |
| nem.Workflow | `WorkflowStep` | record class | `src/nem.Workflow.Domain/Models/NemFlowDefinition.cs` | Depends on `WorkflowStepId` from `nem.Contracts.Identity` and `StepDefinition` polymorphism |
| nem.Workflow | `StepDefinition` | abstract record class | `src/nem.Workflow.Domain/Models/Steps/StepDefinition.cs` | Stable step discriminator contract for flow serialization/execution |
| nem.Sentinel | `RemediationPlan` | record | `src/nem.Sentinel.Domain/Models/RemediationPlan.cs` | Sentinel planning boundary; depends on local remediation/incident/playbook IDs |
| nem.Sentinel | `RemediationStep` | record | `src/nem.Sentinel.Domain/Models/RemediationStep.cs` | Atomic remediation action boundary |
| nem.Sentinel | `RemediationPlannedEvent` | record | `src/nem.Sentinel.Domain/Events/RemediationPlannedEvent.cs` | Cross-boundary remediation event candidate |
| nem.Sentinel | `RemediationExecutedEvent` | record | `src/nem.Sentinel.Domain/Events/RemediationExecutedEvent.cs` | Execution audit boundary |
| nem.Sentinel | `RemediationOutcomeRecordedEvent` | record | `src/nem.Sentinel.Domain/Events/RemediationOutcomeRecordedEvent.cs` | Outcome/audit boundary |

## Strongly-typed IDs used in scope

### nem.Mimir-typed-ids local IDs

| ID | Location | Underlying type | Edge |
|---|---|---|---|
| `ArenaConfigId` | `src/nem.Mimir.Domain/ValueObjects/ArenaConfigId.cs` | `Guid` | Mimir-local configuration aggregate identity |
| `AuditEntryId` | `src/nem.Mimir.Domain/ValueObjects/AuditEntryId.cs` | `Guid` | Mimir-local audit identity |
| `ConversationId` | `src/nem.Mimir.Domain/ValueObjects/ConversationId.cs` | `Guid` | Mimir-local conversation boundary |
| `EvaluationFeedbackId` | `src/nem.Mimir.Domain/ValueObjects/EvaluationFeedbackId.cs` | `Guid` | Mimir-local evaluation feedback identity |
| `EvaluationId` | `src/nem.Mimir.Domain/ValueObjects/EvaluationId.cs` | `Guid` | Mimir-local evaluation identity |
| `FolderId` | `src/nem.Mimir.Domain/ValueObjects/FolderId.cs` | `Guid` | Mimir-local folder identity |
| `KnowledgeCollectionId` | `src/nem.Mimir.Domain/ValueObjects/KnowledgeCollectionId.cs` | `Guid` | Mimir-local knowledge collection identity |
| `LeaderboardEntryId` | `src/nem.Mimir.Domain/ValueObjects/LeaderboardEntryId.cs` | `Guid` | Mimir-local leaderboard identity |
| `MessageId` | `src/nem.Mimir.Domain/ValueObjects/MessageId.cs` | `Guid` | Mimir-local message identity |
| `ModelProfileId` | `src/nem.Mimir.Domain/ValueObjects/ModelProfileId.cs` | `Guid` | Mimir-local model profile identity |
| `SystemPromptId` | `src/nem.Mimir.Domain/ValueObjects/SystemPromptId.cs` | `Guid` | Mimir-local prompt identity |
| `UserId` | `src/nem.Mimir.Domain/ValueObjects/UserId.cs` | `Guid` | Mimir-local user aggregate identity |
| `UserMemoryId` | `src/nem.Mimir.Domain/ValueObjects/UserMemoryId.cs` | `Guid` | Mimir-local memory identity |
| `UserPreferenceId` | `src/nem.Mimir.Domain/ValueObjects/UserPreferenceId.cs` | `Guid` | Mimir-local preference identity |

### Shared `nem.Contracts` IDs used by Mimir

| ID | Namespace | Representative usage | Edge |
|---|---|---|---|
| `CorrelationId` | `nem.Contracts.Identity` | `src/nem.Mimir.Application/Agents/Selection/Steps/EmbeddingRankerStep.cs` | Shared tracing/request correlation boundary |
| `InferenceProviderId` | `nem.Contracts.Inference` | `src/nem.Mimir.Infrastructure/Inference/InferenceGatewayService.cs` | Shared provider identity for inference integration |
| `SkillId` | `nem.Contracts.Identity` | `src/nem.Mimir.Infrastructure/Persistence/TrajectoryRecorder.cs` | Shared skill boundary from ecosystem contracts |
| `SkillVersionId` | `nem.Contracts.Identity` | `src/nem.Mimir.Infrastructure/Persistence/TrajectoryRecorder.cs` | Shared skill-version boundary |

### Workflow IDs from `nem.Contracts`

| ID | Location in contracts | Representative workflow usage | Edge |
|---|---|---|---|
| `WorkflowId` | `../nem.Contracts/src/nem.Contracts/Identity/WorkflowId.cs` | `src/nem.Workflow.Domain/Models/NemFlowDefinition.cs` | Canonical flow definition identity |
| `WorkflowRunId` | `../nem.Contracts/src/nem.Contracts/Identity/WorkflowRunId.cs` | `src/nem.Workflow.Infrastructure/Handlers/ExecuteStepCommand.cs` | Canonical workflow run identity |
| `WorkflowStepId` | `../nem.Contracts/src/nem.Contracts/Identity/WorkflowStepId.cs` | `src/nem.Workflow.Domain/Models/NemFlowDefinition.cs` | Canonical workflow step identity |
| `WorkflowTriggerId` | `../nem.Contracts/src/nem.Contracts/Identity/WorkflowTriggerId.cs` | `src/nem.Workflow.Infrastructure/Triggers/TriggerRegistrationService.cs` | Canonical trigger identity |
| `WorkflowVersionId` | `../nem.Contracts/src/nem.Contracts/Identity/WorkflowVersionId.cs` | `src/nem.Workflow.Domain/Entities/WorkflowVersion.cs` | Canonical workflow version identity |

### nem.Sentinel local IDs

| ID | Location | Underlying type | Edge |
|---|---|---|---|
| `CooldownId` | `src/nem.Sentinel.Domain/Models/CooldownId.cs` | `Guid` | Sentinel-local cooldown identity |
| `HealthSignalId` | `src/nem.Sentinel.Domain/Models/HealthSignalId.cs` | `Guid` | Sentinel-local health signal identity |
| `IncidentId` | `src/nem.Sentinel.Domain/Models/IncidentId.cs` | `Guid` | Sentinel-local incident identity |
| `PlaybookId` | `src/nem.Sentinel.Domain/Models/PlaybookId.cs` | `Guid` | Sentinel-local playbook identity |
| `RemediationId` | `src/nem.Sentinel.Domain/Models/RemediationId.cs` | `Guid` | Sentinel-local remediation identity |

### Shared `nem.Contracts` IDs used by Sentinel

| ID | Namespace | Representative usage | Edge |
|---|---|---|---|
| `CorrelationId` | `nem.Contracts.Identity` | `src/nem.Sentinel.Infrastructure/Organism/Handlers/OrganismStateChangedHandler.cs` | Shared organism/event correlation boundary |
| `HomeostasisCorrectionId` | `nem.Contracts.Identity` | `src/nem.Sentinel.Infrastructure/Organism/Homeostasis/SentinelHomeostasisAgent.cs` | Shared organism correction identity |
| `OrganismHeartbeatId` | `nem.Contracts.Identity` | `src/nem.Sentinel.Infrastructure/Organism/Heartbeat/SentinelOrganismHeartbeat.cs` | Shared organism heartbeat identity |
| `SecurityScanId` | `nem.Contracts.Identity` | `src/nem.Sentinel.Infrastructure/Monitor/SkillSecurityScanCompletedEventConsumer.cs` | Shared skill-security event identity |
| `SkillId` | `nem.Contracts.Identity` | `src/nem.Sentinel.Infrastructure/Monitor/SkillDriftDetectedEventConsumer.cs` | Shared skill boundary |
| `SkillSubmissionId` | `nem.Contracts.Identity` | `src/nem.Sentinel.Infrastructure/Monitor/SkillSecurityScanCompletedEventConsumer.cs` | Shared skill-submission boundary |

## Cross-repo dependency edges to preserve

1. **Mimir -> shared contracts only**
   - Mimir application/runtime contracts do not reference Workflow or Sentinel source directly.
   - Shared identifiers currently cross boundaries through `nem.Contracts` (`CorrelationId`, `SkillId`, `SkillVersionId`, `InferenceProviderId`).

2. **Workflow -> shared contracts only**
   - Workflow definition and execution IDs are fully pinned to `nem.Contracts.Identity`.
   - Workflow domain models remain free of direct Mimir and Sentinel types.

3. **Sentinel -> shared contracts + remediation events**
   - Sentinel planning/execution remains locally modeled with its own remediation IDs.
   - Sentinel consumes ecosystem contract IDs for organism/skill/security integration.
   - Remediation events are the stable export surface for future Workflow/Mimir bridges.

## Notable baseline risks

- Mimir still carries its own local `ITypedId<Guid>` plus local ID set instead of using `nem.Contracts` end-to-end.
- Workflow already uses shared contract IDs consistently and is the cleanest cross-repo baseline.
- Sentinel mixes local remediation IDs with shared ecosystem IDs; this is acceptable only if anti-corruption adapters stay explicit.
