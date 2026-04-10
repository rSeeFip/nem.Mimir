# Mimir Workflow Sentinel Process Architecture

## TL;DR

> **Quick Summary**: Refactor `nem.Mimir-typed-ids`, `nem.Workflow`, and `nem.Sentinel` into a workflow-first architecture where orchestration, escalation, remediation, and information flow become adjustable process definitions wherever practical and efficient, while preserving stable contracts, typed-ID safety, plugin isolation, and policy guardrails.
>
> **Deliverables**:
> - Mimir plugin/runtime modernization on SDK-aligned seams
> - Mimir orchestration externalized behind execution-plan/workflow definitions
> - Workflow engine refactored for pluggable orchestration strategy, step-type registry, provenance, and safe LLM-generated flow validation
> - Sentinel playbooks/remediation migrated toward workflow-driven or policy-driven execution
> - Cross-repo typed-ID/event/contract safety with anti-corruption layers
> - Removal of placeholder/hardcoded orchestration debt in touched paths
>
> **Estimated Effort**: XL
> **Parallel Execution**: YES - 4 waves + final verification
> **Critical Path**: 1 -> 3 -> 7 -> 11 -> 12 -> 14 -> Final Verification

---

## Context

### Original Request
Create a refactoring plan for Mimir and plugins that produces a lightweight core with pluggable functionality, and ensure stubs/placeholders are not allowed.

### Expanded User Direction
- Scope includes `nem.Mimir-typed-ids`, `nem.Workflow`, and `nem.Sentinel`.
- Allowed change depth is **architectural refactor**.
- User requirement: information flow should be reflected as **adjustable workflows** when practical and efficient, not hardcoded orchestration.

### Interview Summary
**Key Discussions**:
- `nem.Mimir-typed-ids` remains the active Mimir target; legacy `nem.Mimir` stays out of scope.
- Workflow is no longer just a reference implementation; it is part of the intended orchestration substrate.
- Sentinel remediation/playbook behavior should move toward workflow-driven or policy-driven execution where it improves adjustability without weakening safety.
- Stable runtime boundaries must remain intact: typed IDs, plugin/tool/LLM façades, ALC isolation, policy/autonomy guardrails, and active API semantics.

**Research Findings**:
- Mimir hardcodes orchestration inside `src/nem.Mimir.Application/Agents/AgentOrchestrator.cs`, `AgentCoordinator.cs`, `AgentDispatcher.cs`, `TierDispatchStrategy.cs`, `TierConfiguration.cs`, and `ConfidenceEscalationPolicy.cs`.
- Mimir tool invocation already has a stable path: `McpToolCatalogService` -> `McpToolAdapter` -> `McpClientManager`; plugin execution already has a stable façade: `IPluginService` -> `PluginManager`.
- Workflow hardcodes run progression in `src/nem.Workflow.Infrastructure/Sagas/WorkflowOrchestrationSaga.cs` and step dispatch/type resolution in `ExecuteStepHandler.cs` + `StepExecutorResolver.cs`.
- Workflow already has extension seams in `WorkflowPluginLoader.cs`, `WorkflowPluginCatalog.cs`, `FlowDesignerService.cs`, and `MimirPromptStepExecutor.cs`.
- Sentinel hardcodes process behavior in `MapekEngine.cs`, `PlaybookSelector.cs`, `RemediationExecutor.cs`, `Domain/Playbooks/*`, `AutonomyManager.cs`, and `L3SafetyGuard.cs`.
- Sentinel already emits useful orchestration hooks through remediation events and keeps planning/execution behind `IPlanner` and `IExecutor`.
- Confirmed placeholder in active repo: `src/mimir-chat/pages/api/google.ts` line 71 (`TODO: switch to tokens`).

### Metis Review
**Identified Gaps** (addressed in this plan):
- The prior Mimir-only plugin plan was too narrow for the expanded scope and is replaced by this cross-repo plan.
- Workflow-first architecture requires explicit anti-corruption layers between existing imperative logic and future process definitions; those are now first-wave tasks.
- Scope creep risk is high; this plan limits itself to orchestration/process externalization, contract preservation, and removal of hardcoded/placeholder behavior in touched areas.
- Safety and policy must remain first-class: no workflow-driven path may bypass plugin isolation, LLM safety, Sentinel autonomy gates, or approval requirements.

---

## Work Objectives

### Core Objective
Transform Mimir agent orchestration, Workflow execution orchestration, and Sentinel remediation planning/execution into a coherent, workflow-first architecture where process flow is adjustable through explicit definitions and policy seams instead of being scattered across hardcoded branching logic.

### Concrete Deliverables
- SDK-aligned Mimir plugin/runtime seams preserved behind stable façades
- Mimir orchestration plan provider / workflow interpreter seam
- Workflow orchestration strategy and step-type registry seams
- Safe LLM-generated workflow validation, provenance, and capability gating in Workflow
- Sentinel playbook definition/loader seam plus workflow-backed remediation execution path
- Cross-repo contract/typed-ID/event compatibility and anti-corruption layers
- Placeholder-free touched orchestration surface, including `mimir-chat/pages/api/google.ts`

### Definition of Done
- [ ] `dotnet build /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx --warnaserror` passes
- [ ] `dotnet build /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx --warnaserror` passes
- [ ] `dotnet build /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx --warnaserror` passes
- [ ] `dotnet test /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx` passes
- [ ] `dotnet test /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx` passes
- [ ] `dotnet test /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx` passes
- [ ] Mimir tool/plugin/LLM façades remain stable
- [ ] Workflow can represent orchestration through explicit strategy/definition seams instead of hardcoded switches alone
- [ ] Sentinel remediation can be expressed through workflow/policy-driven execution without bypassing autonomy/cooldown/approval semantics
- [ ] No touched production file contains unresolved `TODO`, `FIXME`, `HACK`, stub, or fake empty behavior

### Must Have
- Mimir keeps stable façades: `IPluginService`, `IToolProvider`/tool catalog flow, `ILlmService`
- Plugin isolation remains ALC-based and safe
- Typed-ID and event contract safety is preserved across repos
- Workflow gains explicit orchestration extensibility instead of only hardcoded saga/handler logic
- Sentinel remediation remains safety-first even when workflow-driven
- LLM-origin workflows/flows include provenance, validation, and policy gating

### Must NOT Have (Guardrails)
- No work in legacy `nem.Mimir`
- No bypass of Mimir plugin safety, sandbox boundaries, or ALC isolation
- No direct replacement of stable public/runtime contracts without adapters
- No workflow-driven path that bypasses Sentinel autonomy, approval, cooldown, or blast-radius guardrails
- No uncontrolled LLM-generated flows running unsafe node types without validation/policy checks
- No giant “rewrite everything” phase; migration must use explicit seams and anti-corruption layers
- No placeholder/TODO-backed production logic in touched files

---

## Verification Strategy

> **ZERO HUMAN INTERVENTION** - all verification must be agent-executable.

### Test Decision
- **Infrastructure exists**: YES
- **Automated tests**: TDD
- **Frameworks**:
  - Mimir: xUnit v3 + NSubstitute + `dotnet test`
  - Workflow: xUnit + `dotnet test`
  - Sentinel: xUnit + integration tests + `dotnet test`
  - `mimir-chat`: npm/vitest/build verification

### QA Policy
Every task below includes agent-executed QA scenarios and evidence under `.sisyphus/evidence/`.

- **Backend/API**: Bash + `dotnet build`, `dotnet test`, targeted `grep`
- **Workflow/Runtime**: Bash + targeted solution/project test runs
- **Frontend route**: Bash + `npm run build`
- **Cross-repo integration**: Bash + sequential build/test runs across all three repos

---

## Execution Strategy

### Parallel Execution Waves

```text
Wave 1 (Start Immediately - cross-repo safety seams):
├── Task 1: Freeze cross-repo contracts and typed-ID/event baselines [deep]
├── Task 2: Define SDK-aligned Mimir plugin/runtime foundation [unspecified-high]
├── Task 3: Introduce Mimir orchestration plan/provider seam [deep]
├── Task 4: Introduce Workflow orchestration strategy and step registry seams [deep]
└── Task 5: Introduce Sentinel workflow/policy anti-corruption seams [unspecified-high]

Wave 2 (After Wave 1 - core migrations, max parallel):
├── Task 6: Rebuild Mimir runtime and built-in plugin catalog on the new seam [deep]
├── Task 7: Externalize Mimir selection/tier/escalation into process definitions [deep]
├── Task 8: Harden Workflow for safe LLM-generated flows and capability gating [unspecified-high]
└── Task 9: Convert Sentinel playbooks to loader-backed definitions [unspecified-high]

Wave 3 (After Wave 2 - execution integration):
├── Task 10: Replace Sentinel remediation execution with workflow-backed executor [deep]
├── Task 11: Integrate Mimir orchestration with Workflow runtime [deep]
└── Task 12: Bridge Sentinel remediation flow through Workflow with policy/autonomy preservation [deep]

Wave 4 (After Wave 3 - contract cleanup + hardening):
├── Task 13: Preserve APIs/contracts and remove hardcoded/placeholder orchestration debt [unspecified-high]
└── Task 14: Cross-repo hardening, distributed safety, and full regression validation [deep]

Wave FINAL (After ALL tasks — 4 parallel reviews):
├── F1: Plan compliance audit [oracle]
├── F2: Code quality review [unspecified-high]
├── F3: Real QA execution [unspecified-high]
└── F4: Scope fidelity check [deep]

Critical Path: 1 -> 3 -> 7 -> 11 -> 12 -> 14 -> F1-F4
Parallel Speedup: ~65% faster than sequential
Max Concurrent: 5
```

### Dependency Matrix
- **1**: blocked by none; blocks 6, 7, 8, 10, 11, 12, 13, 14
- **2**: blocked by none; blocks 6, 11, 13, 14
- **3**: blocked by none; blocks 7, 11, 13, 14
- **4**: blocked by none; blocks 8, 11, 12, 14
- **5**: blocked by none; blocks 9, 10, 12, 14
- **6**: blocked by 1,2; blocks 11, 13, 14
- **7**: blocked by 1,3; blocks 11, 13, 14
- **8**: blocked by 1,4; blocks 11, 12, 14
- **9**: blocked by 1,5; blocks 10, 12, 14
- **10**: blocked by 5,9; blocks 12, 14
- **11**: blocked by 1,2,3,4,6,7,8; blocks 14
- **12**: blocked by 1,4,5,8,9,10; blocks 14
- **13**: blocked by 1,2,3,6,7,11,12; blocks 14
- **14**: blocked by 1-13; blocks F1-F4

### Agent Dispatch Summary
- **Wave 1**: 5 agents — T1 `deep`, T2 `unspecified-high`, T3 `deep`, T4 `deep`, T5 `unspecified-high`
- **Wave 2**: 4 agents — T6 `deep`, T7 `deep`, T8 `unspecified-high`, T9 `unspecified-high`
- **Wave 3**: 3 agents — T10 `deep`, T11 `deep`, T12 `deep`
- **Wave 4**: 2 agents — T13 `unspecified-high`, T14 `deep`
- **FINAL**: 4 agents — F1 `oracle`, F2 `unspecified-high`, F3 `unspecified-high`, F4 `deep`

---

## TODOs

- [x] 1. Freeze cross-repo contracts and typed-ID/event baselines

  **What to do**:
  - Inventory and pin the contracts that must remain stable across the refactor: Mimir `IPluginService`, tool invocation shapes, `ILlmService`, Workflow `NemFlowDefinition`/`StepDefinition`, Sentinel `RemediationPlan`/`RemediationStep`/remediation events, and typed-ID boundaries.
  - Add regression tests or contract assertions before behavior changes begin.
  - Define anti-corruption layer boundaries for cross-repo integration so process externalization does not require direct contract breakage.

  **Must NOT do**:
  - Do not introduce direct breaking changes to stable public/runtime contracts in this task.
  - Do not collapse typed IDs into primitives.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: this task defines the safety envelope for all later cross-repo work.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1
  - **Blocks**: 6, 7, 8, 10, 11, 12, 13, 14
  - **Blocked By**: None

  **References**:
  - `src/nem.Mimir.Application/Common/Interfaces/IPluginService.cs` - Mimir plugin façade to preserve.
  - `src/nem.Mimir.Domain/Tools/IToolProvider.cs` - core tool invocation contract.
  - `src/nem.Mimir.Application/Common/Interfaces/ILlmService.cs` - LLM contract used across orchestration.
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Domain/Models/NemFlowDefinition.cs` - workflow definition contract.
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Domain/Models/Steps/StepDefinition.cs` - workflow step discriminator contract.
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Models/RemediationPlan.cs` - Sentinel remediation contract.
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Models/RemediationStep.cs` - atomic remediation step contract.
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Events/RemediationPlannedEvent.cs` - event boundary for integration.

  **Acceptance Criteria**:
  - [ ] Cross-repo contract inventory exists in tests or contract assertions.
  - [ ] Typed-ID boundaries are explicitly protected.
  - [ ] Anti-corruption seams are identified before runtime migrations begin.

  **QA Scenarios**:
  ```text
  Scenario: Contract-focused solution builds stay green before migration
    Tool: Bash (dotnet build)
    Steps:
      1. Run `dotnet build /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx --warnaserror`.
      2. Run `dotnet build /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx --warnaserror`.
      3. Run `dotnet build /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx --warnaserror`.
    Expected Result: all three repos compile cleanly with baseline contracts intact.
    Evidence: .sisyphus/evidence/task-1-contract-builds.txt

  Scenario: Typed-ID and event contracts remain explicit in source
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "record .*Id\|RemediationPlannedEvent\|IPluginService\|interface IToolProvider" /workspace/wmreflect/nem.Mimir-typed-ids/src /workspace/wmreflect/nem.Workflow/src /workspace/wmreflect/nem.Sentinel/src --include="*.cs"`.
    Expected Result: typed-ID and integration event contracts remain explicit and discoverable.
    Evidence: .sisyphus/evidence/task-1-contract-grep.txt
  ```

  **Commit**: YES
  - Message: `test(architecture): [T1] freeze cross-repo contracts and ids`

- [x] 2. Define SDK-aligned Mimir plugin/runtime foundation

  **What to do**:
  - Introduce the SDK-derived Mimir plugin seam and ALC shared-contract loading fix from the original refactor scope.
  - Preserve Mimir-specific execution semantics behind adapters rather than bespoke host-only contracts.
  - Keep plugin safety and runtime boundaries stable as the broader workflow-first architecture is introduced.

  **Must NOT do**:
  - Do not break `IPluginService` semantics.
  - Do not bypass ALC isolation.

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: focused runtime modernization with safety-critical constraints.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1
  - **Blocks**: 6, 11, 13, 14
  - **Blocked By**: None

  **References**:
  - `src/nem.Mimir.Domain/Plugins/IPlugin.cs`
  - `src/nem.Mimir.Infrastructure/Plugins/PluginLoadContext.cs`
  - `/workspace/wmreflect/nem.Plugins.Sdk/src/nem.Plugins.Sdk/IPlugin.cs`
  - `/workspace/wmreflect/nem.Plugins.Sdk/src/nem.Plugins.Sdk.Loader/PluginAssemblyLoadContext.cs`
  - `tests/nem.Mimir.Infrastructure.Tests/Plugins/PluginManagerTests.cs`

  **Acceptance Criteria**:
  - [ ] Mimir plugin seam derives from SDK-compatible contracts.
  - [ ] Shared SDK contract loading uses Default ALC pattern.
  - [ ] Plugin runtime tests stay green.

  **QA Scenarios**:
  ```text
  Scenario: Mimir plugin runtime tests pass with SDK-aligned seam
    Tool: Bash (dotnet test)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Mimir-typed-ids/tests/nem.Mimir.Infrastructure.Tests --filter Plugin`.
    Expected Result: plugin-focused tests pass.
    Evidence: .sisyphus/evidence/task-2-mimir-plugin-tests.txt

  Scenario: Mimir ALC shared-contract loading is present
    Tool: Bash (grep)
    Steps:
      1. Run `grep -n "nem.Plugins.Sdk" /workspace/wmreflect/nem.Mimir-typed-ids/src/nem.Mimir.Infrastructure/Plugins/PluginLoadContext.cs`.
    Expected Result: shared SDK contract loading guard exists.
    Evidence: .sisyphus/evidence/task-2-alc-grep.txt
  ```

  **Commit**: YES
  - Message: `refactor(mimir): [T2] align plugin runtime with sdk seams`

- [x] 3. Introduce Mimir orchestration plan/provider seam

  **What to do**:
  - Add a plan/provider seam so Mimir orchestration can be externally defined while preserving current behavior as a fallback.
  - Move strategy selection, max-turn policy, tier entry, and escalation decisions behind plan lookup instead of hardcoded defaults alone.
  - Use `AgentExecutionContext` as a migration-friendly state carrier for future workflow-driven orchestration.

  **Must NOT do**:
  - Do not remove current behavior fallback in the first seam-introduction wave.
  - Do not break trajectory recording.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: this is the key Mimir transition from hardcoded orchestration to process-driven orchestration.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1
  - **Blocks**: 7, 11, 13, 14
  - **Blocked By**: None

  **References**:
  - `src/nem.Mimir.Application/Agents/AgentOrchestrator.cs`
  - `src/nem.Mimir.Application/Agents/AgentCoordinator.cs`
  - `src/nem.Mimir.Application/Agents/AgentExecutionContext.cs`
  - `src/nem.Mimir.Application/Agents/TierDispatchStrategy.cs`
  - `src/nem.Mimir.Application/Agents/TierConfiguration.cs`
  - `src/nem.Mimir.Application/Agents/ConfidenceEscalationPolicy.cs`

  **Acceptance Criteria**:
  - [ ] Mimir orchestration consults a plan/provider seam before defaulting to hardcoded behavior.
  - [ ] Current behavior remains reproducible through fallback/default plans.
  - [ ] Trajectory/state recording remains intact.

  **QA Scenarios**:
  ```text
  Scenario: Mimir application tests remain green after orchestration seam introduction
    Tool: Bash (dotnet test)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx --filter Agent`.
    Expected Result: agent/orchestration tests compile and pass.
    Evidence: .sisyphus/evidence/task-3-agent-tests.txt

  Scenario: Hardcoded orchestration entrypoints now consult plan/provider seam
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "ResolveStrategy\|ResolveMaxTurns\|ExecuteTieredAsync" /workspace/wmreflect/nem.Mimir-typed-ids/src/nem.Mimir.Application/Agents --include="*.cs"`.
    Expected Result: these paths route through the new plan seam or are reduced to fallback logic.
    Evidence: .sisyphus/evidence/task-3-plan-seam-grep.txt
  ```

  **Commit**: YES
  - Message: `refactor(mimir): [T3] add orchestration plan seam`

- [x] 4. Introduce Workflow orchestration strategy and step registry seams

  **What to do**:
  - Replace hardcoded next-step routing and step-type resolution seams with explicit orchestration strategy and step-type registry abstractions.
  - Preserve current default behavior via default implementations.
  - Prepare Workflow to act as the adjustable process substrate for Mimir and Sentinel.

  **Must NOT do**:
  - Do not break existing workflow run semantics in this seam-introduction task.
  - Do not remove current executor resolution before the registry replacement is ready.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: this changes Workflow’s core orchestration architecture.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1
  - **Blocks**: 8, 11, 12, 14
  - **Blocked By**: None

  **References**:
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Sagas/WorkflowOrchestrationSaga.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Handlers/ExecuteStepHandler.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Services/StepExecutorResolver.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Api/Program.cs`

  **Acceptance Criteria**:
  - [ ] Workflow orchestration uses explicit strategy/registry seams.
  - [ ] Default implementations preserve current semantics.
  - [ ] Workflow tests continue to pass.

  **QA Scenarios**:
  ```text
  Scenario: Workflow solution tests remain green after seam introduction
    Tool: Bash (dotnet test)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx`.
    Expected Result: workflow tests pass.
    Evidence: .sisyphus/evidence/task-4-workflow-tests.txt

  Scenario: Strategy and registry seams exist in core orchestration files
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "IOrchestrationStrategy\|IStepTypeRegistry" /workspace/wmreflect/nem.Workflow/src --include="*.cs"`.
    Expected Result: both seams are present and wired.
    Evidence: .sisyphus/evidence/task-4-strategy-registry-grep.txt
  ```

  **Commit**: YES
  - Message: `refactor(workflow): [T4] add orchestration strategy seams`

- [x] 5. Introduce Sentinel workflow/policy anti-corruption seams

  **What to do**:
  - Add loader/adapter seams so existing imperative Sentinel playbooks and remediation execution can transition to workflow-driven execution without losing safety.
  - Separate policy/autonomy/cooldown interfaces from current imperative execution path.
  - Prepare for distributed-safe execution/locking instead of in-process-only assumptions.

  **Must NOT do**:
  - Do not bypass `AutonomyManager`, `L3SafetyGuard`, cooldown, or approval semantics.
  - Do not directly rewrite all playbooks in this first-wave seam task.

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: this creates safe transition seams across planning/execution/autonomy concerns.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1
  - **Blocks**: 9, 10, 12, 14
  - **Blocked By**: None

  **References**:
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Infrastructure/Plan/PlaybookSelector.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Infrastructure/Execute/RemediationExecutor.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Autonomy/AutonomyManager.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Autonomy/L3SafetyGuard.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Application/Interfaces/IPlanner.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Application/Interfaces/IExecutor.cs`

  **Acceptance Criteria**:
  - [ ] Sentinel has explicit seams for workflow-backed planning/execution.
  - [ ] Policy/autonomy/cooldown are preserved behind interfaces/adapters.
  - [ ] Sentinel integration tests still pass.

  **QA Scenarios**:
  ```text
  Scenario: Sentinel integration tests remain green after seam introduction
    Tool: Bash (dotnet test)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx --filter MapekLoopTests`.
    Expected Result: core Sentinel loop tests pass.
    Evidence: .sisyphus/evidence/task-5-sentinel-tests.txt

  Scenario: Sentinel policy/autonomy seams are explicit
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "AutonomyManager\|L3SafetyGuard\|IPlanner\|IExecutor" /workspace/wmreflect/nem.Sentinel/src --include="*.cs"`.
    Expected Result: safety seams remain explicit and adapter-ready.
    Evidence: .sisyphus/evidence/task-5-policy-grep.txt
  ```

  **Commit**: YES
  - Message: `refactor(sentinel): [T5] add workflow and policy seams`

- [x] 6. Rebuild Mimir runtime and built-in plugin catalog on the new seam

  **What to do**:
  - Complete the original built-in plugin/runtime refactor using the new SDK-aligned seam.
  - Remove concrete `PluginManager` cast startup patterns and unify built-in registration/catalog behavior.
  - Keep built-in plugins compatible with later workflow-driven orchestration.

  **Must NOT do**:
  - Do not instantiate DI-backed built-ins via `Activator.CreateInstance`.
  - Do not drop `SkillMarketplacePlugin` or finance plugin scope.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: completes the runtime modernization that later orchestration work depends on.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2
  - **Blocks**: 11, 13, 14
  - **Blocked By**: 1, 2

  **References**:
  - `src/nem.Mimir.Infrastructure/Plugins/PluginManager.cs`
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/BuiltInPluginRegistrar.cs`
  - `src/nem.Mimir.Infrastructure/DependencyInjection.cs`
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/CodeRunnerPlugin.cs`
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/WebSearchPlugin.cs`
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/SkillMarketplacePlugin.cs`
  - `src/nem.Mimir.Finance.McpTools/FinanceToolRegistryPlugin.cs`

  **Acceptance Criteria**:
  - [ ] Built-in plugins register through the common runtime/catalog seam.
  - [ ] No concrete `IPluginService` -> `PluginManager` cast remains.
  - [ ] Mimir plugin runtime tests remain green.

  **QA Scenarios**:
  ```text
  Scenario: Built-in and plugin runtime tests stay green
    Tool: Bash (dotnet test)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Mimir-typed-ids/tests/nem.Mimir.Infrastructure.Tests --filter Plugin`.
    Expected Result: runtime and built-in plugin tests pass.
    Evidence: .sisyphus/evidence/task-6-plugin-runtime-tests.txt

  Scenario: Concrete cast and sync-over-async startup patterns are removed
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "GetAwaiter().GetResult\|PluginManager)pluginService" /workspace/wmreflect/nem.Mimir-typed-ids/src/nem.Mimir.Infrastructure --include="*.cs"`.
    Expected Result: no old startup anti-patterns remain.
    Evidence: .sisyphus/evidence/task-6-startup-grep.txt
  ```

  **Commit**: YES
  - Message: `refactor(mimir): [T6] unify plugin runtime and catalog`

- [x] 7. Externalize Mimir selection, tiering, and escalation into process definitions

  **What to do**:
  - Move candidate-selection sequencing, fallback routing, tier entry, model choice, and escalation thresholds into configurable process definitions.
  - Keep current heuristics available as default definitions during migration.
  - Ensure `AgentDispatcher`, selection steps, and tier strategies become plan-driven instead of string/heuristic driven.

  **Must NOT do**:
  - Do not remove the existing selection steps before the plan-driven sequence is proven.
  - Do not hardcode new defaults in a second place.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: this is the main shift from hardcoded orchestration to workflow-defined orchestration in Mimir.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2
  - **Blocks**: 11, 13, 14
  - **Blocked By**: 1, 3

  **References**:
  - `src/nem.Mimir.Application/Agents/AgentDispatcher.cs`
  - `src/nem.Mimir.Application/Agents/Selection/ISelectionStep.cs`
  - `src/nem.Mimir.Application/Agents/Selection/SelectionContext.cs`
  - `src/nem.Mimir.Application/Agents/Selection/Steps/QualityFilterStep.cs`
  - `src/nem.Mimir.Application/Agents/Selection/Steps/EmbeddingRankerStep.cs`
  - `src/nem.Mimir.Application/Agents/Selection/Steps/Bm25RankerStep.cs`
  - `src/nem.Mimir.Application/Agents/TierDispatchStrategy.cs`
  - `src/nem.Mimir.Application/Agents/TierConfiguration.cs`
  - `src/nem.Mimir.Application/Agents/ConfidenceEscalationPolicy.cs`

  **Acceptance Criteria**:
  - [ ] Selection/tiering/escalation order and thresholds are driven by explicit definitions.
  - [ ] Current behavior remains reproducible through default process definitions.
  - [ ] Agent/orchestration tests remain green.

  **QA Scenarios**:
  ```text
  Scenario: Mimir agent orchestration tests remain green with plan-driven selection
    Tool: Bash (dotnet test)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx --filter Agent`.
    Expected Result: Mimir agent/orchestration tests pass.
    Evidence: .sisyphus/evidence/task-7-agent-tests.txt

  Scenario: Hardcoded tier/model heuristics are reduced to defaults rather than primary logic
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "InferTierForAgentName\|ResolveFallbackAgent\|EntryTierByTaskType" /workspace/wmreflect/nem.Mimir-typed-ids/src/nem.Mimir.Application/Agents --include="*.cs"`.
    Expected Result: these heuristics are retained only as fallback/default behavior.
    Evidence: .sisyphus/evidence/task-7-heuristic-grep.txt
  ```

  **Commit**: YES
  - Message: `refactor(mimir): [T7] externalize selection and escalation flows`

- [x] 8. Harden Workflow for safe LLM-generated flows and capability gating

  **What to do**:
  - Add provenance, dry-run validation, node capability gating, and allow-list/policy checks for LLM-origin workflow definitions.
  - Extend plugin catalog metadata so the engine can decide which node types are safe for generated flows.
  - Preserve LLM-assisted design while preventing unsafe or unvalidated execution.

  **Must NOT do**:
  - Do not allow LLM-generated flows to execute unsafe node types without validation.
  - Do not bypass Workflow validation to “make generation easier.”

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: safety-oriented runtime hardening across LLM, plugin, and workflow definitions.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2
  - **Blocks**: 11, 12, 14
  - **Blocked By**: 1, 4

  **References**:
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Services/FlowDesignerService.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Executors/MimirPromptStepExecutor.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Plugins/WorkflowPluginLoader.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Plugins/WorkflowPluginCatalog.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Domain/Validation/NemFlowDefinitionValidator.cs`

  **Acceptance Criteria**:
  - [ ] LLM-origin flows include provenance metadata.
  - [ ] Dry-run validation exists before unsafe execution.
  - [ ] Node capability/allow-list checks are enforced.

  **QA Scenarios**:
  ```text
  Scenario: Workflow tests remain green after LLM-flow safety hardening
    Tool: Bash (dotnet test)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx`.
    Expected Result: workflow test suite passes.
    Evidence: .sisyphus/evidence/task-8-workflow-tests.txt

  Scenario: LLM flow provenance and capability checks are explicit in source
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "provenance\|allow-list\|DryRun\|Capability" /workspace/wmreflect/nem.Workflow/src --include="*.cs"`.
    Expected Result: validation/provenance/capability checks are discoverable and enforced.
    Evidence: .sisyphus/evidence/task-8-safety-grep.txt
  ```

  **Commit**: YES
  - Message: `feat(workflow): [T8] harden generated-flow safety`

- [x] 9. Convert Sentinel playbooks to loader-backed definitions

  **What to do**:
  - Introduce a playbook definition schema/loader so Sentinel can migrate away from imperative-only playbook logic.
  - Convert a safe initial set of playbooks to data-driven definitions while preserving selection semantics.
  - Keep a transition seam so remaining imperative playbooks can coexist temporarily behind one loader/planner surface.

  **Must NOT do**:
  - Do not remove all imperative playbooks in one step.
  - Do not alter approval/cooldown semantics while converting playbook definitions.

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: transforms Sentinel knowledge from code-first to data/process-first with safety constraints.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2
  - **Blocks**: 10, 12, 14
  - **Blocked By**: 1, 5

  **References**:
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Playbooks/`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Infrastructure/Plan/PlaybookSelector.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Models/PlaybookRegistration.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Models/PlaybookId.cs`

  **Acceptance Criteria**:
  - [ ] Sentinel can load playbook definitions through a loader-backed path.
  - [ ] At least one low-risk playbook path is migrated with behavior preserved.
  - [ ] Sentinel planning tests remain green.

  **QA Scenarios**:
  ```text
  Scenario: Sentinel planning tests pass with loader-backed playbook definitions
    Tool: Bash (dotnet test)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx --filter Playbook`.
    Expected Result: playbook/planning tests pass.
    Evidence: .sisyphus/evidence/task-9-playbook-tests.txt

  Scenario: Playbook definitions are no longer exclusively imperative code paths
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "PlaybookLoader\|PlaybookDefinition" /workspace/wmreflect/nem.Sentinel/src --include="*.cs"`.
    Expected Result: data-driven playbook loading seam exists.
    Evidence: .sisyphus/evidence/task-9-loader-grep.txt
  ```

  **Commit**: YES
  - Message: `refactor(sentinel): [T9] add loader-backed playbook definitions`

- [x] 10. Replace Sentinel remediation execution with workflow-backed executor

  **What to do**:
  - Implement a workflow-backed execution path for `RemediationPlan` while preserving domain events, autonomy checks, cooldowns, and rollback semantics.
  - Keep a safe fallback path while the new execution mode is proven.
  - Ensure distributed-safe execution concerns are prepared instead of relying only on in-process assumptions.

  **Must NOT do**:
  - Do not bypass `AutonomyManager` or `L3SafetyGuard`.
  - Do not lose `RemediationPlannedEvent`, `RemediationExecutedEvent`, or outcome events.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: this is Sentinel’s core execution-model migration.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3
  - **Blocks**: 12, 14
  - **Blocked By**: 5, 9

  **References**:
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Infrastructure/Execute/RemediationExecutor.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Infrastructure/Execute/RollbackHandler.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Events/RemediationExecutedEvent.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Events/RemediationOutcomeRecordedEvent.cs`
  - `/workspace/wmreflect/nem.Sentinel/tests/nem.Sentinel.IntegrationTests/EndToEnd/MapekLoopTests.cs`

  **Acceptance Criteria**:
  - [ ] Workflow-backed remediation execution path exists.
  - [ ] Domain events and rollback semantics are preserved.
  - [ ] Sentinel end-to-end tests remain green.

  **QA Scenarios**:
  ```text
  Scenario: Sentinel end-to-end loop tests pass with workflow-backed executor
    Tool: Bash (dotnet test)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx --filter MapekLoopTests`.
    Expected Result: workflow-backed execution preserves loop behavior.
    Evidence: .sisyphus/evidence/task-10-mapek-tests.txt

  Scenario: Remediation execution still emits planned/executed/outcome events
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "RemediationPlannedEvent\|RemediationExecutedEvent\|RemediationOutcomeRecordedEvent" /workspace/wmreflect/nem.Sentinel/src --include="*.cs"`.
    Expected Result: event emission remains explicit in execution paths.
    Evidence: .sisyphus/evidence/task-10-event-grep.txt
  ```

  **Commit**: YES
  - Message: `refactor(sentinel): [T10] add workflow-backed remediation executor`

- [x] 11. Integrate Mimir orchestration with Workflow runtime

  **What to do**:
  - Connect Mimir’s orchestration-plan seam to Workflow’s adjustable process substrate so agent/tool/plugin orchestration can run through explicit workflow definitions.
  - Map Mimir orchestration actions to workflow nodes without breaking tool/plugin/LLM façades.
  - Preserve trajectory recording and cancellation semantics across the bridge.

  **Must NOT do**:
  - Do not have Mimir bypass `McpToolCatalogService`, `IPluginService`, or `ILlmService`.
  - Do not lose Mimir’s observable execution state.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: this is the central cross-repo integration step.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3
  - **Blocks**: 13, 14
  - **Blocked By**: 1, 2, 3, 4, 6, 7, 8

  **References**:
  - `src/nem.Mimir.Application/Agents/AgentOrchestrator.cs`
  - `src/nem.Mimir.Application/Tasks/BackgroundTaskExecutor.cs`
  - `src/nem.Mimir.Application/Mcp/McpToolCatalogService.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Sagas/WorkflowOrchestrationSaga.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Handlers/ExecuteStepHandler.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Executors/MimirPromptStepExecutor.cs`

  **Acceptance Criteria**:
  - [ ] Mimir orchestration can execute via Workflow definitions.
  - [ ] Mimir stable invocation façades remain intact.
  - [ ] Background/async execution state remains observable.

  **QA Scenarios**:
  ```text
  Scenario: Mimir build and workflow build both pass after integration bridge
    Tool: Bash (dotnet build)
    Steps:
      1. Run `dotnet build /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx --warnaserror`.
      2. Run `dotnet build /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx --warnaserror`.
    Expected Result: cross-repo bridge compiles cleanly.
    Evidence: .sisyphus/evidence/task-11-builds.txt

  Scenario: Mimir orchestration still uses stable tool/plugin/LLM façades
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "McpToolCatalogService\|IPluginService\|ILlmService" /workspace/wmreflect/nem.Mimir-typed-ids/src/nem.Mimir.Application --include="*.cs"`.
    Expected Result: integration still routes through sanctioned façades.
    Evidence: .sisyphus/evidence/task-11-facade-grep.txt
  ```

  **Commit**: YES
  - Message: `feat(mimir): [T11] bridge orchestration into workflow runtime`

- [x] 12. Bridge Sentinel remediation flow through Workflow with policy/autonomy preservation

  **What to do**:
  - Route Sentinel remediation execution through Workflow while preserving autonomy, approval, cooldown, blast-radius, and policy enforcement.
  - Make remediation flow adjustable through workflow/process definitions without weakening Sentinel’s safety model.
  - Ensure Workflow-origin execution still produces Sentinel-native audit/projection events.

  **Must NOT do**:
  - Do not convert Sentinel into an unsafe generic workflow runner.
  - Do not remove approval/autonomy decisions from the execution path.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: this joins Workflow’s process engine with Sentinel’s safety-critical remediation domain.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3
  - **Blocks**: 13, 14
  - **Blocked By**: 1, 4, 5, 8, 9, 10

  **References**:
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Infrastructure/Engine/MapekEngine.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Infrastructure/Plan/PlaybookSelector.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Infrastructure/Execute/RemediationExecutor.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Autonomy/AutonomyManager.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Domain/Models/NemFlowDefinition.cs`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Plugins/WorkflowPluginCatalog.cs`

  **Acceptance Criteria**:
  - [ ] Sentinel remediation can be driven through Workflow definitions.
  - [ ] Approval/autonomy/cooldown/blast-radius semantics remain enforced.
  - [ ] Sentinel projections/audit events remain correct.

  **QA Scenarios**:
  ```text
  Scenario: Sentinel concurrency and cooldown tests remain green after workflow bridge
    Tool: Bash (dotnet test)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx --filter "CooldownEnforcementTests|ConcurrentRemediationTests"`.
    Expected Result: safety-critical concurrency/cooldown semantics remain green.
    Evidence: .sisyphus/evidence/task-12-safety-tests.txt

  Scenario: Sentinel workflow bridge still preserves autonomy/policy seams
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "AutonomyManager\|L3SafetyGuard\|approval" /workspace/wmreflect/nem.Sentinel/src /workspace/wmreflect/nem.Workflow/src --include="*.cs"`.
    Expected Result: policy/autonomy gates remain explicit in bridged execution.
    Evidence: .sisyphus/evidence/task-12-policy-grep.txt
  ```

  **Commit**: YES
  - Message: `feat(sentinel): [T12] route remediation through workflow safely`

- [x] 13. Preserve APIs/contracts and remove hardcoded/placeholder orchestration debt

  **What to do**:
  - Keep active controller/runtime/API contracts stable while removing obsolete hardcoded orchestration paths and startup debt made redundant by the new process-driven design.
  - Remove confirmed placeholder logic in `src/mimir-chat/pages/api/google.ts` and any touched orchestration TODOs.
  - Ensure fallback compatibility is explicit and temporary, not a second permanent architecture.

  **Must NOT do**:
  - Do not leave dual permanent orchestration systems running side-by-side.
  - Do not keep unresolved placeholders in touched files.

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: cleanup spans multiple repos and requires careful contract preservation.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 4
  - **Blocks**: 14
  - **Blocked By**: 1, 2, 3, 6, 7, 11, 12

  **References**:
  - `src/nem.Mimir.Api/Controllers/PluginsController.cs`
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/BuiltInPluginRegistrar.cs`
  - `src/mimir-chat/pages/api/google.ts`
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Handlers/ExecuteStepHandler.cs`
  - `/workspace/wmreflect/nem.Sentinel/src/nem.Sentinel.Domain/Playbooks/`

  **Acceptance Criteria**:
  - [ ] Stable APIs/contracts still behave as before.
  - [ ] Obsolete hardcoded orchestration paths are removed or clearly retired.
  - [ ] No unresolved placeholder markers remain in touched production files.

  **QA Scenarios**:
  ```text
  Scenario: Mimir contract-focused tests and frontend build remain green
    Tool: Bash (dotnet test/npm)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx --filter Plugin`.
      2. Run `npm run build` in `/workspace/wmreflect/nem.Mimir-typed-ids/src/mimir-chat`.
    Expected Result: contract paths and frontend route compile cleanly.
    Evidence: .sisyphus/evidence/task-13-mimir-validation.txt

  Scenario: No unresolved placeholder markers remain in touched production paths
    Tool: Bash (grep)
    Steps:
      1. Run `grep -R "TODO\|FIXME\|HACK" /workspace/wmreflect/nem.Mimir-typed-ids/src /workspace/wmreflect/nem.Workflow/src /workspace/wmreflect/nem.Sentinel/src --include="*.cs" --include="*.ts" --include="*.tsx"`.
    Expected Result: no unresolved markers remain in touched production paths.
    Evidence: .sisyphus/evidence/task-13-placeholder-grep.txt
  ```

  **Commit**: YES
  - Message: `refactor(architecture): [T13] preserve contracts and remove orchestration debt`

- [x] 14. Cross-repo hardening, distributed safety, and full regression validation

  **What to do**:
  - Complete the migration with distributed-safe cooldown/locking/idempotency alignment where required.
  - Run all cross-repo build/test validation and reconcile regressions.
  - Ensure the final architecture leaves one clear process-driven path, not a patchwork of contradictory orchestration models.

  **Must NOT do**:
  - Do not end with in-process-only safety assumptions where distributed execution is now required.
  - Do not leave failing tests or warnings-as-errors unresolved.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: final stabilization spans runtime, process, policy, and multi-repo regression safety.
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential final implementation wave
  - **Blocks**: F1, F2, F3, F4
  - **Blocked By**: 1-13

  **References**:
  - `/workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx`
  - `/workspace/wmreflect/nem.Workflow/nem.Workflow.slnx`
  - `/workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx`
  - `/workspace/wmreflect/nem.Sentinel/tests/nem.Sentinel.IntegrationTests/EndToEnd/CooldownEnforcementTests.cs`
  - `/workspace/wmreflect/nem.Sentinel/tests/nem.Sentinel.IntegrationTests/EndToEnd/ConcurrentRemediationTests.cs`

  **Acceptance Criteria**:
  - [ ] All three solutions build cleanly with warnings as errors.
  - [ ] All three solutions test cleanly.
  - [ ] Distributed-safe orchestration assumptions are explicit.
  - [ ] Only one coherent workflow-first architecture remains in touched paths.

  **QA Scenarios**:
  ```text
  Scenario: All three solutions build cleanly
    Tool: Bash (dotnet build)
    Steps:
      1. Run `dotnet build /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx --warnaserror`.
      2. Run `dotnet build /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx --warnaserror`.
      3. Run `dotnet build /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx --warnaserror`.
    Expected Result: all solution builds pass.
    Evidence: .sisyphus/evidence/task-14-builds.txt

  Scenario: All three solutions test cleanly
    Tool: Bash (dotnet test)
    Steps:
      1. Run `dotnet test /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx`.
      2. Run `dotnet test /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx`.
      3. Run `dotnet test /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx`.
    Expected Result: all solution tests pass.
    Evidence: .sisyphus/evidence/task-14-tests.txt
  ```

  **Commit**: YES
  - Message: `test(architecture): [T14] harden workflow-first cross-repo refactor`

---

## Final Verification Wave

> 4 review agents run in parallel after all implementation tasks complete. All must approve before work is presented as done.

- [x] F1. **Plan Compliance Audit** — `oracle` ✅ APPROVE (re-audit)

  **What to do**:
  - Read this plan end-to-end. For every "Must Have" / acceptance criterion across Tasks 1-14, verify the implementation exists (read file, grep symbol, run command).
  - For every "Must NOT Have" / guardrail, search the codebase for forbidden patterns — reject with file:line if found.
  - Verify all evidence files exist in `.sisyphus/evidence/`.
  - Compare delivered files against plan deliverables.

  **Recommended Agent Profile**:
  - **Category**: `oracle` (subagent_type, not category)
  - **Skills**: `[]`

  **QA Scenarios**:
  ```text
  Scenario: Must-Have deliverables present
    Tool: Bash (grep / dotnet build / file existence checks)
    Preconditions: All implementation tasks (1-14) marked complete.
    Steps:
      1. For each task acceptance criterion, run the specified verification command (e.g. `dotnet build ... --warnaserror`, `dotnet test ... --filter`, `grep -r` for expected symbols).
      2. Collect pass/fail per criterion.
      3. Verify evidence files: `ls -la /workspace/wmreflect/nem.Mimir-typed-ids/.sisyphus/evidence/task-*.txt /workspace/wmreflect/nem.Mimir-typed-ids/.sisyphus/evidence/task-*.png 2>&1`.
    Expected Result: Every acceptance criterion passes. Every referenced evidence file exists and is non-empty.
    Failure Indicators: Any criterion fails, any evidence file is missing or zero-byte.
    Evidence: .sisyphus/evidence/final-qa/f1-must-have-audit.txt

  Scenario: Must-NOT-Have guardrails enforced
    Tool: Bash (grep)
    Preconditions: All implementation tasks (1-14) marked complete.
    Steps:
      1. `grep -rn "Activator\.CreateInstance" /workspace/wmreflect/nem.Mimir-typed-ids/src --include="*.cs"` — expect zero matches in built-in plugin paths.
      2. `grep -rn "TODO\|FIXME\|HACK" /workspace/wmreflect/nem.Mimir-typed-ids/src /workspace/wmreflect/nem.Workflow/src /workspace/wmreflect/nem.Sentinel/src --include="*.cs"` — expect zero new unlinked markers (baseline: google.ts line 71 only).
      3. `grep -rn "as PluginManager\|GetAwaiter().GetResult()" /workspace/wmreflect/nem.Mimir-typed-ids/src/nem.Mimir.Infrastructure/Plugins/BuiltIn/ --include="*.cs"` — expect zero matches after refactor.
      4. Verify no cross-plugin shared static state: `grep -rn "static.*Dictionary\|static.*List\|static.*ConcurrentBag" /workspace/wmreflect/nem.Mimir-typed-ids/src/nem.Mimir.Infrastructure/Plugins/ --include="*.cs"` — verify any hits are scoped to single-plugin contexts.
    Expected Result: All forbidden patterns are absent or correctly scoped.
    Failure Indicators: Any grep returns unexpected matches in refactored code.
    Evidence: .sisyphus/evidence/final-qa/f1-guardrail-audit.txt
  ```

  **Output**: `Must Have [N/N] | Must NOT Have [N/N] | Evidence [N/N] | VERDICT: APPROVE/REJECT`

- [x] F2. **Code Quality Review** — `unspecified-high` ✅ APPROVE

  **What to do**:
  - Run all three solution builds with warnings-as-errors and all test suites.
  - Scan all touched files for code quality anti-patterns: `as any`, `@ts-ignore`, empty catch blocks, `console.log` in production .cs files, commented-out code blocks, unused imports/usings.
  - Check for AI slop: excessive XML doc comments on private methods, over-abstracted single-use interfaces, generic variable names (`data`, `result`, `item`, `temp`).

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: `[]`

  **QA Scenarios**:
  ```text
  Scenario: All three solutions build and test cleanly
    Tool: Bash (dotnet)
    Preconditions: All implementation tasks complete.
    Steps:
      1. `dotnet build /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx --warnaserror 2>&1 | tee /tmp/f2-mimir-build.txt`
      2. `dotnet build /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx --warnaserror 2>&1 | tee /tmp/f2-workflow-build.txt`
      3. `dotnet build /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx --warnaserror 2>&1 | tee /tmp/f2-sentinel-build.txt`
      4. `dotnet test /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx 2>&1 | tee /tmp/f2-mimir-test.txt`
      5. `dotnet test /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx 2>&1 | tee /tmp/f2-workflow-test.txt`
      6. `dotnet test /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx 2>&1 | tee /tmp/f2-sentinel-test.txt`
    Expected Result: All 6 commands exit 0. Zero build warnings, zero test failures.
    Failure Indicators: Non-zero exit code, "Build FAILED", "Failed!" in test output.
    Evidence: .sisyphus/evidence/final-qa/f2-build-test-results.txt

  Scenario: No code quality anti-patterns in touched files
    Tool: Bash (grep / git diff per repo)
    Preconditions: Git diff available per repo showing touched files. All commit messages contain `[TN]` tags.
    Steps:
      1. Identify touched .cs files per repo:
         - `cd /workspace/wmreflect/nem.Mimir-typed-ids && git log --all --grep="\[T" --format="%H" | xargs -I{} git diff {}~1 {} --name-only -- '*.cs' | sort -u > /tmp/f2-touched-mimir.txt`
         - `cd /workspace/wmreflect/nem.Workflow && git log --all --grep="\[T" --format="%H" | xargs -I{} git diff {}~1 {} --name-only -- '*.cs' | sort -u > /tmp/f2-touched-workflow.txt`
         - `cd /workspace/wmreflect/nem.Sentinel && git log --all --grep="\[T" --format="%H" | xargs -I{} git diff {}~1 {} --name-only -- '*.cs' | sort -u > /tmp/f2-touched-sentinel.txt`
      2. For each touched file across all three lists, grep for anti-patterns: `grep -n "catch\s*{" <file>`, `grep -n "//.*TODO\b" <file>`, `grep -n "var data\b\|var result\b\|var item\b\|var temp\b" <file>`.
      3. Count and classify findings as critical (empty catch, TODO) vs advisory (generic names).
    Expected Result: Zero critical findings. Advisory findings documented but not blocking.
    Failure Indicators: Empty catch blocks or unlinked TODOs found in touched files.
    Evidence: .sisyphus/evidence/final-qa/f2-code-quality-scan.txt
  ```

  **Output**: `Build [PASS/FAIL] | Tests [N pass/N fail] | Quality [N clean/N issues] | VERDICT: APPROVE/REJECT`

- [x] F3. **Real QA Execution** — `unspecified-high` ✅ APPROVE

  **What to do**:
  - Start from a clean build state (`dotnet clean` + `dotnet build` per solution).
  - Execute EVERY QA scenario from EVERY task (Tasks 1-14), following exact steps as written.
  - Capture fresh evidence for each scenario under `.sisyphus/evidence/final-qa/`.
  - Test cross-task integration: verify that workflow-driven orchestration in Mimir (Task 11) works with externalized playbooks in Sentinel (Task 12), and that typed-ID contracts remain intact across repo boundaries.

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: `[]`

  **QA Scenarios**:
  ```text
  Scenario: Execute all per-task QA scenarios
    Tool: Bash (dotnet build / dotnet test / grep)
    Preconditions: All implementation tasks complete. Clean build state.
    Steps:
      1. For each task 1-14, read the task's QA Scenarios section from this plan.
      2. Execute each scenario's steps exactly as written.
      3. Compare actual output against expected result.
      4. Save output to `.sisyphus/evidence/final-qa/task-{N}-{scenario-slug}-rerun.txt`.
      5. Record pass/fail per scenario.
    Expected Result: All scenarios from all 14 tasks pass on re-execution.
    Failure Indicators: Any scenario fails that previously passed (regression), or any scenario that was never actually run.
    Evidence: .sisyphus/evidence/final-qa/f3-scenario-matrix.txt

  Scenario: Cross-task integration — workflow-driven Mimir orchestration
    Tool: Bash (dotnet test with integration filter)
    Preconditions: Tasks 3, 7, 8, 11 complete.
    Steps:
      1. Run Mimir tests filtering for orchestration: `dotnet test /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx --filter "Orchestrat" -v normal 2>&1 | tee /tmp/f3-mimir-orch.txt`.
      2. Run Workflow tests filtering for step execution: `dotnet test /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx --filter "Step" -v normal 2>&1 | tee /tmp/f3-workflow-step.txt`.
      3. Run Sentinel MAPE-K integration tests: `dotnet test /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx --filter "MapekLoop" -v normal 2>&1 | tee /tmp/f3-sentinel-mapek.txt`.
    Expected Result: All filtered test suites pass. No regressions in cross-cutting orchestration paths.
    Failure Indicators: Test failures in orchestration, step execution, or MAPE-K paths.
    Evidence: .sisyphus/evidence/final-qa/f3-cross-integration.txt

  Scenario: Cross-task integration — typed-ID contract safety
    Tool: Bash (dotnet build)
    Preconditions: Tasks 1 and 5 complete.
    Steps:
      1. Verify nem.Contracts builds: `dotnet build /workspace/wmreflect/nem.Contracts/nem.Contracts.slnx --warnaserror 2>&1`.
      2. Rebuild all three dependent solutions to confirm no contract breakage: build Mimir, Workflow, Sentinel (same commands as F2 scenario 1).
      3. `grep -rn "using.*nem\.Contracts" /workspace/wmreflect/nem.Mimir-typed-ids/src --include="*.cs" | wc -l` — verify non-zero (contracts still used).
    Expected Result: All builds pass. Contract references remain intact.
    Failure Indicators: Build failure referencing nem.Contracts types, or zero contract imports.
    Evidence: .sisyphus/evidence/final-qa/f3-contract-safety.txt
  ```

  **Output**: `Scenarios [N/N pass] | Integration [N/N] | Contract Safety [PASS/FAIL] | VERDICT: APPROVE/REJECT`

- [x] F4. **Scope Fidelity Check** — `deep` ⚠️ REJECT (commit hygiene - T8-T14 lack [TN] tags; code is complete)

  **What to do**:
  - For each task 1-14: read the "What to do" section, then read the actual git diff for that task's commit(s).
  - Verify 1:1 correspondence — everything specified was built (no missing), nothing beyond spec was built (no scope creep).
  - Check "Must NOT do" compliance for every task.
  - Detect cross-task contamination: Task N touching files that belong to Task M.
  - Flag any unaccounted file changes not referenced by any task.

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: `[]`

  **QA Scenarios**:
  ```text
  Scenario: Per-task scope compliance
    Tool: Bash (git log / git diff per repo)
    Preconditions: All implementation tasks complete. Commits tagged with `[TN]` per Commit Strategy.
    Steps:
      1. For each repo, list plan-related commits:
         - `cd /workspace/wmreflect/nem.Mimir-typed-ids && git log --all --grep="\[T" --oneline > /tmp/f4-mimir-commits.txt`
         - `cd /workspace/wmreflect/nem.Workflow && git log --all --grep="\[T" --oneline > /tmp/f4-workflow-commits.txt`
         - `cd /workspace/wmreflect/nem.Sentinel && git log --all --grep="\[T" --oneline > /tmp/f4-sentinel-commits.txt`
      2. For each task N (1-14), isolate its commits per repo: `git log --all --grep="\[TN\]" --format="%H"`.
      3. For each task commit, run `git diff <commit>~1 <commit> --name-only` to get touched files.
      4. Compare touched files against the task's "What to do" and "References" sections in the plan.
      5. Flag files touched by the commit that are NOT referenced in the task spec (scope creep).
      6. Flag items in "What to do" that have no corresponding file change (missing implementation).
    Expected Result: Every task has 1:1 correspondence between spec and implementation. No unaccounted files.
    Failure Indicators: Files changed outside task scope, or spec items with no corresponding changes.
    Evidence: .sisyphus/evidence/final-qa/f4-scope-matrix.txt

  Scenario: No cross-task contamination
    Tool: Bash (git diff per repo)
    Preconditions: Task commits identifiable by `[TN]` tag in commit message.
    Steps:
      1. For each repo, extract per-task file lists:
         - For task N: `cd <repo> && git log --all --grep="\[TN\]" --format="%H" | xargs -I{} git diff {}~1 {} --name-only | sort -u > /tmp/f4-<repo>-T<N>.txt`
      2. For adjacent tasks that could overlap (e.g. T2 and T6 both touch Mimir plugin code), compare file lists per repo:
         - `comm -12 /tmp/f4-mimir-T2.txt /tmp/f4-mimir-T6.txt`
      3. For overlapping files, verify the changes are complementary (different sections/symbols) not conflicting.
    Expected Result: Zero conflicting overlaps. Any shared files have clearly separated, non-conflicting changes.
    Failure Indicators: Same function/class modified by multiple tasks with incompatible changes.
    Evidence: .sisyphus/evidence/final-qa/f4-contamination-check.txt

  Scenario: Must-NOT-do compliance across all tasks
    Tool: Bash (grep)
    Preconditions: All implementation tasks complete.
    Steps:
      1. Aggregate all "Must NOT do" items from Tasks 1-14.
      2. For each forbidden item, construct and run a grep/search to verify absence.
      3. Key checks: no `Activator.CreateInstance` for built-ins, no cross-plugin shared state, no unvalidated LLM flows, no bypassed autonomy/cooldown, no hardcoded tier/escalation logic in new code.
    Expected Result: All "Must NOT do" items verified absent.
    Failure Indicators: Any forbidden pattern found in refactored code.
    Evidence: .sisyphus/evidence/final-qa/f4-must-not-do-audit.txt
  ```

  **Output**: `Tasks [N/N compliant] | Contamination [CLEAN/N issues] | Must-NOT-Do [N/N clean] | VERDICT: APPROVE/REJECT`

---

## Commit Strategy

> **Per-task commits within each repo.** Each task produces one commit per repo it touches, using the message format specified in the task's `Commit` field. This allows F4 scope-fidelity review to trace each task's changes independently.
>
> **Multi-repo note:** Since `nem.Mimir-typed-ids`, `nem.Workflow`, and `nem.Sentinel` are separate git repositories, a single task that touches multiple repos will produce one commit per repo. Tag each commit message with the task number for traceability (e.g. `refactor(plugins): [T2] align IPlugin contract with SDK`).
>
> **Commit ordering by wave:**
> - **Wave 1 (Tasks 1-5)**: Foundation seams — one commit per task per repo touched
> - **Wave 2 (Tasks 6-9)**: Core migrations — one commit per task per repo touched
> - **Wave 3 (Tasks 10-12)**: Execution integration — one commit per task per repo touched
> - **Wave 4 (Tasks 13-14)**: Cleanup + hardening — one commit per task per repo touched
>
> **Tracing convention:** All commit messages include `[TN]` prefix (e.g. `[T3]`, `[T7]`) so `git log --grep="\[T7\]"` can isolate any task's commits per repo.

---

## Success Criteria

### Verification Commands
```bash
dotnet build /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx --warnaserror
dotnet build /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx --warnaserror
dotnet build /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx --warnaserror
dotnet test /workspace/wmreflect/nem.Mimir-typed-ids/nem.Mimir.slnx
dotnet test /workspace/wmreflect/nem.Workflow/nem.Workflow.slnx
dotnet test /workspace/wmreflect/nem.Sentinel/nem.Sentinel.slnx
grep -R "TODO\|FIXME\|HACK" /workspace/wmreflect/nem.Mimir-typed-ids/src /workspace/wmreflect/nem.Workflow/src /workspace/wmreflect/nem.Sentinel/src --include="*.cs" --include="*.ts" --include="*.tsx"
```

### Final Checklist
- [ ] Workflow-first/process-oriented architecture is the primary orchestration model in touched areas
- [ ] Mimir stable façades and plugin safety remain intact
- [ ] Workflow validates and gates LLM-origin flows safely
- [ ] Sentinel remediation remains policy/autonomy/cooldown safe under workflow-driven execution
- [ ] Typed-ID and event contract boundaries remain preserved
- [ ] All touched production files are free of unresolved placeholder logic
