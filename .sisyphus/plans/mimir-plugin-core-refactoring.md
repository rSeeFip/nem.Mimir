# Mimir Plugin Core Refactoring

## TL;DR

> **Quick Summary**: Refactor `nem.Mimir-typed-ids` from a bespoke plugin runtime into a lightweight host core that uses `nem.Plugins.Sdk` conventions, while preserving the existing Mimir plugin API surface and eliminating placeholder/TODO-backed behavior.
>
> **Deliverables**:
> - SDK-aligned Mimir plugin abstraction and runtime adapter
> - ALC-safe plugin loading with shared contract identity preserved
> - Built-in plugins moved behind a common registration/catalog path
> - Preserved `PluginsController` + `IPluginService` behavior for load/list/execute/unload
> - Placeholder sweep for active plugin-related surface, including `mimir-chat/pages/api/google.ts`
>
> **Estimated Effort**: Large
> **Parallel Execution**: YES - 3 waves + final verification
> **Critical Path**: Task 1 -> Task 2 -> Task 4 -> Task 7 -> Task 9 -> Task 10 -> Final Verification

---

## Context

### Original Request
Create a refactoring plan for Mimir and plugins that produces a lightweight core with pluggable functionality, and ensure stubs/placeholders are not allowed.

### Interview Summary
**Key Discussions**:
- Active implementation target is `nem.Mimir-typed-ids`; legacy `nem.Mimir` must not receive architecture work.
- Current Mimir plugin runtime duplicates behavior that already exists in `nem.Plugins.Sdk` and `nem.Plugins.Sdk.Loader`.
- Built-in plugins are currently hard-wired into startup and registered through a concrete `PluginManager` cast.
- The refactor must preserve host safety, ALC isolation, and current plugin REST behavior.

**Research Findings**:
- `src/nem.Mimir.Domain/Plugins/IPlugin.cs` exposes Mimir-specific execution semantics (`ExecuteAsync`, `PluginContext`, `PluginResult`) that do not match the base SDK interface.
- `src/nem.Mimir.Infrastructure/Plugins/PluginLoadContext.cs` lacks the SDK pattern that loads `nem.Plugins.Sdk*` assemblies from the Default ALC.
- `src/nem.Mimir.Infrastructure/DependencyInjection.cs` hard-wires built-ins and hosts `BuiltInPluginRegistrar`.
- `src/nem.Mimir.Api/Controllers/PluginsController.cs` already dispatches through Wolverine `IMessageBus`; this behavior must remain stable.
- Active built-ins to migrate include `CodeRunnerPlugin`, `WebSearchPlugin`, `SkillMarketplacePlugin`, and `FinanceToolRegistryPlugin`.
- Confirmed placeholder in active repo: `src/mimir-chat/pages/api/google.ts` line 71 (`TODO: switch to tokens`).

### Metis Review
**Identified Gaps** (addressed in this plan):
- ALC type-identity protection was missing from the earlier outline and is now an explicit first-wave task.
- `SkillMarketplacePlugin` and `nem.Mimir.Finance.McpTools` were missing from initial migration scope and are now included.
- The plan now preserves the current `IPluginService` façade and plugin REST contract instead of expanding scope into controller/CQRS redesign.
- The plan now treats built-ins as DI-backed plugin contributions, not directory-loaded `Activator.CreateInstance` plugins.

---

## Work Objectives

### Core Objective
Replace Mimir's bespoke plugin runtime with an SDK-aligned adapter architecture so the host core becomes thinner, plugin loading is safer, built-ins use the same contribution model as external plugins, and no touched production path contains placeholder or stub behavior.

### Concrete Deliverables
- `nem.Mimir-typed-ids` plugin abstraction aligned to `nem.Plugins.Sdk`
- SDK-safe ALC loading behavior in the active repo
- SDK-backed plugin runtime adapter behind `IPluginService`
- Common registration/catalog path for built-in and external plugins
- Preserved API/application plugin operations (`Load`, `List`, `Execute`, `Unload`)
- Updated tests for runtime, built-ins, and finance plugin package
- Placeholder-free active plugin-related surface

### Definition of Done
- [ ] `dotnet build nem.Mimir.slnx --warnaserror` passes
- [ ] `dotnet test nem.Mimir.slnx` passes
- [ ] `PluginsController` endpoints still support load/list/execute/unload semantics
- [ ] No plugin startup path casts `IPluginService` to concrete `PluginManager`
- [ ] No touched plugin-related file contains unresolved `TODO`, `FIXME`, `HACK`, stub, or fake empty behavior

### Must Have
- ALC isolation preserved using SDK-style shared-contract loading
- `IPluginService` retained as Mimir's application façade
- Mimir-specific execution semantics preserved via an adapter/extension contract
- Built-in plugins registered through the same catalog/runtime seam as external plugins
- `CodeRunnerPlugin`, `WebSearchPlugin`, `SkillMarketplacePlugin`, and `FinanceToolRegistryPlugin` included in scope
- API behavior preserved for `Load`, `List`, `Execute`, and `Unload`

### Must NOT Have (Guardrails)
- No work in legacy `/workspace/wmreflect/nem.Mimir` except documentation-only note or explicit decommission planning outside this refactor
- No CQRS/messaging redesign; preserve existing Wolverine-based dispatch behavior in the active repo
- No forced migration of DI-backed built-ins through `Activator.CreateInstance`
- No indefinite compatibility layer that leaves both old and new plugin models active long-term
- No placeholder/TODO-backed production logic in touched files

---

## Verification Strategy

> **ZERO HUMAN INTERVENTION** - all verification must be agent-executable.

### Test Decision
- **Infrastructure exists**: YES
- **Automated tests**: TDD
- **Framework**: xUnit v3 + NSubstitute + `dotnet test`
- **If TDD**: each wave starts by tightening or adding tests that capture the intended post-refactor contract before changing production code

### QA Policy
Every task below includes agent-executed QA scenarios and concrete evidence outputs under `.sisyphus/evidence/`.

- **Backend/API**: Bash + `dotnet test`, `dotnet build`, `curl`
- **Runtime/Library**: Bash + targeted test runs
- **Frontend API route**: Bash + repo frontend test command if present, otherwise focused static verification + route execution command
- **No manual sign-off** is allowed as acceptance criteria

---

## Execution Strategy

### Parallel Execution Waves

```text
Wave 1 (Start Immediately - contract + safety foundations):
├── Task 1: Define SDK-aligned Mimir plugin seam [deep]
├── Task 2: Fix ALC shared-contract loading and add safety tests [unspecified-high]
└── Task 3: Freeze current API/application plugin contract with regression tests [quick]

Wave 2 (After Wave 1 - runtime migration, max parallel):
├── Task 4: Rebuild PluginManager/IPluginService as SDK-backed adapter [deep]
├── Task 5: Migrate DI-backed built-in plugins to common contribution contract [unspecified-high]
└── Task 6: Migrate finance plugin package and plugin metadata flow [quick]

Wave 3 (After Wave 2 - integration + cleanup):
├── Task 7: Replace startup wiring with catalog/hosted-loader pattern [deep]
├── Task 8: Preserve controller/command execution path and end-to-end plugin operations [unspecified-high]
├── Task 9: Remove placeholder/TODO-backed active-surface behavior [quick]
└── Task 10: Final regression hardening and dead-code cleanup [unspecified-high]

Wave FINAL (After ALL implementation tasks — 4 parallel reviews):
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Code quality review (unspecified-high)
├── Task F3: Real QA execution (unspecified-high)
└── Task F4: Scope fidelity check (deep)

Critical Path: 1 -> 2 -> 4 -> 7 -> 9 -> 10 -> F1-F4
Parallel Speedup: ~55-65% faster than strictly sequential execution
Max Concurrent: 3
```

### Dependency Matrix

- **1**: Blocked By none -> Blocks 4, 5, 6, 7
- **2**: Blocked By none -> Blocks 4, 7, 10
- **3**: Blocked By none -> Blocks 8, 10
- **4**: Blocked By 1, 2 -> Blocks 7, 8, 10
- **5**: Blocked By 1 -> Blocks 7, 8, 10
- **6**: Blocked By 1 -> Blocks 7, 10
- **7**: Blocked By 1, 2, 4, 5, 6 -> Blocks 8, 9, 10
- **8**: Blocked By 3, 4, 5, 7 -> Blocks 10
- **9**: Blocked By 7 -> Blocks 10
- **10**: Blocked By 2, 3, 4, 5, 6, 7, 8, 9 -> Blocks F1-F4

### Agent Dispatch Summary

- **Wave 1**: 3 agents - T1 `deep`, T2 `unspecified-high`, T3 `quick`
- **Wave 2**: 3 agents - T4 `deep`, T5 `unspecified-high`, T6 `quick`
- **Wave 3**: 4 agents - T7 `deep`, T8 `unspecified-high`, T9 `quick`, T10 `unspecified-high`
- **Final**: 4 agents - F1 `oracle`, F2 `unspecified-high`, F3 `unspecified-high`, F4 `deep`

---

## TODOs

- [ ] 1. Define SDK-aligned Mimir plugin seam

  **What to do**:
  - Introduce a Mimir-specific plugin contract that extends `nem.Plugins.Sdk.IPlugin` while preserving Mimir execution semantics (`ExecuteAsync`, `PluginContext`, `PluginResult`, metadata identity, shutdown/lifecycle needs).
  - Decide the minimal compatibility path for the current `nem.Mimir.Domain.Plugins.IPlugin`: either replace it with the new SDK-derived contract or mark it `[Obsolete]` and route all active implementations onto the SDK-derived interface in the same wave.
  - Add/update domain-level types so plugin identity and manifest data remain available without requiring the old bespoke interface shape.
  - Tighten tests around contract expectations before runtime migration begins.

  **Must NOT do**:
  - Do not keep two long-term active plugin models in production.
  - Do not remove `PluginContext`, `PluginResult`, or `PluginMetadata` semantics.
  - Do not push execution logic into controllers or handlers.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: this is the architectural seam for the whole refactor and mistakes here will cascade into every later task.
  - **Skills**: `[]`
  - **Skills Evaluated but Omitted**:
    - `fullstack-dev`: not needed because this task is contract/runtime-shape design, not cross-stack implementation.
    - `git-master`: not needed because this is planning, not git archaeology.

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 2, 3)
  - **Blocks**: 4, 5, 6, 7
  - **Blocked By**: None

  **References**:
  - `src/nem.Mimir.Domain/Plugins/IPlugin.cs` - current Mimir plugin contract that must be reworked into an SDK-aligned seam.
  - `src/nem.Mimir.Domain/Plugins/PluginContext.cs` - current execution input contract that must remain part of Mimir plugin behavior.
  - `src/nem.Mimir.Domain/Plugins/PluginResult.cs` - execution result contract that later runtime tasks must continue returning.
  - `src/nem.Mimir.Domain/Plugins/PluginMetadata.cs` - current metadata shape used by application/API flows and list/load responses.
  - `/workspace/wmreflect/nem.Plugins.Sdk/src/nem.Plugins.Sdk/IPlugin.cs` - required base interface every final plugin contract must extend.
  - `/workspace/wmreflect/nem.Plugins.Sdk/src/nem.Plugins.Sdk/IPluginManifest.cs` - reference for separating plugin identity/manifest concerns from execution behavior.
  - `/workspace/wmreflect/nem.Plugins.Sdk/src/nem.Plugins.Sdk/IPluginLifecycle.cs` - reference for startup/shutdown semantics instead of bespoke host-only assumptions.
  - `src/nem.Mimir.Application/Common/Interfaces/IPluginService.cs` - confirms the public application façade that must stay stable while internals change.
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/CodeRunnerPlugin.cs` - representative built-in implementation to validate the new contract shape against a real plugin.

  **Acceptance Criteria**:
  - [ ] A single SDK-derived Mimir plugin seam is defined and documented in code for all active plugin implementations.
  - [ ] No active plugin implementation in the repo is left depending solely on the old bespoke interface shape.
  - [ ] `PluginContext`, `PluginResult`, and `PluginMetadata` remain valid contracts for runtime/application use.
  - [ ] `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter Plugin` passes after the contract changes.

  **QA Scenarios**:
  ```text
  Scenario: SDK-derived seam compiles against current plugin abstractions
    Tool: Bash (dotnet test)
    Preconditions: Task 1 implementation is complete in working tree
    Steps:
      1. Run `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter Plugin` from repo root.
      2. Confirm test output includes zero failed tests and no compile errors referencing plugin contracts.
      3. Save command output to `.sisyphus/evidence/task-1-plugin-contract-tests.txt`.
    Expected Result: plugin-focused infrastructure tests compile and pass against the new seam.
    Failure Indicators: CS0246/CS0535/interface mismatch errors, or failing plugin contract tests.
    Evidence: .sisyphus/evidence/task-1-plugin-contract-tests.txt

  Scenario: Old bespoke-only contract is no longer the active production seam
    Tool: Bash (grep)
    Preconditions: Task 1 implementation is complete in working tree
    Steps:
      1. Run `grep -R "interface IPlugin" -n src/nem.Mimir.Domain src/nem.Mimir.Infrastructure --include="*.cs"`.
      2. Run `grep -R "nem.Mimir.Domain.Plugins.IPlugin" -n src tests --include="*.cs"`.
      3. Save outputs to `.sisyphus/evidence/task-1-contract-grep.txt` and verify usages route through the SDK-derived seam or intentional compatibility marker.
    Expected Result: the repo shows one authoritative active seam and no accidental split between old and new production interfaces.
    Failure Indicators: multiple active incompatible plugin seams or unchanged direct usage of the old interface in production paths.
    Evidence: .sisyphus/evidence/task-1-contract-grep.txt
  ```

  **Evidence to Capture**:
  - [ ] `.sisyphus/evidence/task-1-plugin-contract-tests.txt`
  - [ ] `.sisyphus/evidence/task-1-contract-grep.txt`

  **Commit**: YES
  - Message: `refactor(plugins): define sdk-aligned mimir plugin seam`
  - Files: `src/nem.Mimir.Domain/Plugins/*`, related tests
  - Pre-commit: `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter Plugin`

- [ ] 2. Fix ALC shared-contract loading and add safety tests

  **What to do**:
  - Update `PluginLoadContext` so `nem.Plugins.Sdk*` assemblies are resolved from the default ALC, matching the workspace SDK loader pattern and preventing type identity breaks.
  - Add/expand tests that prove plugins implementing the SDK-based Mimir contract are discoverable and assignable after load.
  - Verify unload behavior still works after the shared-contract resolution change.

  **Must NOT do**:
  - Do not disable collectible ALC behavior.
  - Do not load arbitrary host assemblies from default ALC beyond the shared SDK contract family.
  - Do not weaken host-safety behavior on plugin load failure.

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: focused runtime surgery plus targeted regression tests, but less architectural ambiguity than Task 1.
  - **Skills**: `[]`
  - **Skills Evaluated but Omitted**:
    - `fullstack-dev`: no frontend/backend integration scope here.
    - `playwright`: no browser behavior involved.

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 3)
  - **Blocks**: 4, 7, 10
  - **Blocked By**: None

  **References**:
  - `src/nem.Mimir.Infrastructure/Plugins/PluginLoadContext.cs` - active load context missing the shared SDK contract guard.
  - `/workspace/wmreflect/nem.Plugins.Sdk/src/nem.Plugins.Sdk.Loader/PluginAssemblyLoadContext.cs` - canonical pattern for loading `nem.Plugins.Sdk*` assemblies from the default ALC.
  - `tests/nem.Mimir.Infrastructure.Tests/Plugins/PluginManagerTests.cs` - existing positive plugin loading/unloading test coverage to extend.
  - `tests/nem.Mimir.Infrastructure.Tests/Plugins/PluginManagerNegativeTests.cs` - negative-path coverage that must remain valid after ALC changes.
  - `src/nem.Mimir.Infrastructure/Plugins/PluginManager.cs` - consuming runtime that depends on successful assignability/type identity after load.

  **Acceptance Criteria**:
  - [ ] `PluginLoadContext` special-cases `nem.Plugins.Sdk*` assemblies through the default ALC.
  - [ ] A regression test proves a plugin implementing the SDK-derived Mimir seam is assignable after loading.
  - [ ] Existing negative tests for bad plugin loads still pass.
  - [ ] Plugin unload behavior still passes under test after the ALC change.

  **QA Scenarios**:
  ```text
  Scenario: Shared SDK contracts load with correct type identity
    Tool: Bash (dotnet test)
    Preconditions: Task 2 implementation is complete in working tree
    Steps:
      1. Run `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter PluginManagerTests`.
      2. Confirm output includes passing coverage for plugin load/execute/unload paths.
      3. Save output to `.sisyphus/evidence/task-2-pluginmanager-tests.txt`.
    Expected Result: plugin load tests pass without type identity or assignability failures.
    Failure Indicators: `IsAssignableFrom`-style failures, reflection load exceptions, or unload regressions.
    Evidence: .sisyphus/evidence/task-2-pluginmanager-tests.txt

  Scenario: Negative plugin loading paths still fail safely
    Tool: Bash (dotnet test)
    Preconditions: Task 2 implementation is complete in working tree
    Steps:
      1. Run `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter PluginManagerNegativeTests`.
      2. Confirm failures are handled by tests as expected and the suite ends green.
      3. Save output to `.sisyphus/evidence/task-2-pluginmanager-negative-tests.txt`.
    Expected Result: bad plugin inputs remain isolated and do not crash host-oriented tests.
    Failure Indicators: unexpected host crash, changed exception surface, or red negative suite.
    Evidence: .sisyphus/evidence/task-2-pluginmanager-negative-tests.txt
  ```

  **Evidence to Capture**:
  - [ ] `.sisyphus/evidence/task-2-pluginmanager-tests.txt`
  - [ ] `.sisyphus/evidence/task-2-pluginmanager-negative-tests.txt`

  **Commit**: YES
  - Message: `fix(plugins): align alc contract loading with sdk`
  - Files: `src/nem.Mimir.Infrastructure/Plugins/PluginLoadContext.cs`, plugin runtime tests
  - Pre-commit: `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter PluginManager`

- [ ] 3. Freeze current API/application plugin contract with regression tests

  **What to do**:
  - Add regression tests around `IPluginService`-backed load/list/execute/unload behavior before runtime internals are rebuilt.
  - Cover the current request/response contract for `PluginsController` and the application command/query boundary so later refactors cannot silently change API semantics.
  - Capture plugin metadata and execution-result expectations that downstream callers rely on.

  **Must NOT do**:
  - Do not redesign controller routes, request models, or Wolverine dispatch shape in this task.
  - Do not move business logic into API tests only; keep reusable coverage at the application/runtime seam where possible.
  - Do not broaden this into auth/policy redesign.

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: this is focused regression-test scaffolding around known existing behavior.
  - **Skills**: `[]`
  - **Skills Evaluated but Omitted**:
    - `playwright`: browser automation is unnecessary because this is API/controller regression coverage.
    - `fullstack-dev`: no new stack integration is being introduced.

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 2)
  - **Blocks**: 8, 10
  - **Blocked By**: None

  **References**:
  - `src/nem.Mimir.Application/Common/Interfaces/IPluginService.cs` - façade whose public behavior must remain stable throughout the refactor.
  - `src/nem.Mimir.Application/Plugins/Commands/LoadPlugin.cs` - command path for plugin load behavior to pin with tests.
  - `src/nem.Mimir.Application/Plugins/Commands/ExecutePlugin.cs` - command path for execution behavior and result mapping.
  - `src/nem.Mimir.Application/Plugins/Commands/UnloadPlugin.cs` - command path for unload behavior.
  - `src/nem.Mimir.Application/Plugins/Queries/ListPlugins.cs` - query path for list semantics and metadata shape.
  - `src/nem.Mimir.Api/Controllers/PluginsController.cs` - route/response contract that must stay stable.
  - `src/nem.Mimir.Domain/Plugins/PluginMetadata.cs` - expected API/application metadata shape.
  - `src/nem.Mimir.Domain/Plugins/PluginResult.cs` - expected API/application execution result shape.

  **Acceptance Criteria**:
  - [ ] Regression tests exist for load/list/execute/unload behavior before runtime migration tasks begin.
  - [ ] Tests explicitly pin controller route/response expectations or handler contract expectations for all four operations.
  - [ ] `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter Plugin` passes with the new regression coverage.
  - [ ] No production behavior is intentionally changed in this task.

  **QA Scenarios**:
  ```text
  Scenario: Plugin regression suite locks current load/list/execute/unload behavior
    Tool: Bash (dotnet test)
    Preconditions: Task 3 test additions are complete in working tree
    Steps:
      1. Run `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter Plugin`.
      2. Confirm new regression cases are discovered and pass.
      3. Save output to `.sisyphus/evidence/task-3-plugin-regression-tests.txt`.
    Expected Result: plugin-focused suite passes and establishes a stable safety net for later runtime changes.
    Failure Indicators: missing/discovered tests not running, failing route/contract assertions, or accidental production changes breaking baseline behavior.
    Evidence: .sisyphus/evidence/task-3-plugin-regression-tests.txt

  Scenario: Controller contract stays on the same REST shape
    Tool: Bash (grep)
    Preconditions: Task 3 is complete in working tree
    Steps:
      1. Run `grep -n "\[HttpPost\]\|\[HttpGet\]\|\[HttpDelete" src/nem.Mimir.Api/Controllers/PluginsController.cs`.
      2. Run `grep -n "InvokeAsync" src/nem.Mimir.Api/Controllers/PluginsController.cs`.
      3. Save output to `.sisyphus/evidence/task-3-controller-contract-grep.txt` and verify the controller still exposes load/list/execute/unload via the same route shape.
    Expected Result: route attributes and Wolverine dispatch points remain intact while tests now cover them.
    Failure Indicators: changed route shape, removed execute/unload path, or unexpected controller drift.
    Evidence: .sisyphus/evidence/task-3-controller-contract-grep.txt
  ```

  **Evidence to Capture**:
  - [ ] `.sisyphus/evidence/task-3-plugin-regression-tests.txt`
  - [ ] `.sisyphus/evidence/task-3-controller-contract-grep.txt`

  **Commit**: YES
  - Message: `test(plugins): freeze api and application plugin behavior`
  - Files: plugin command/query/controller tests
  - Pre-commit: `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter Plugin`

- [ ] 4. Rebuild PluginManager and IPluginService as an SDK-backed adapter

  **What to do**:
  - Refactor Mimir's `PluginManager` so `IPluginService` becomes an adapter over SDK-style plugin loading/lifecycle management instead of directly owning bespoke reflection/activation logic.
  - Preserve the current `LoadPluginAsync`, `ListPluginsAsync`, `ExecutePluginAsync`, and `UnloadPluginAsync` application contract while switching internals to the SDK-derived plugin seam.
  - Remove or replace the synchronous `RegisterPlugin(IPlugin)` shortcut with an async-safe registration path that works for built-ins and external plugins without `.GetAwaiter().GetResult()`.
  - Keep plugin failures isolated and returned as `PluginResult.Failure(...)` where current behavior requires it.

  **Must NOT do**:
  - Do not remove `IPluginService` from the application layer.
  - Do not change the public behavior of load/list/execute/unload operations.
  - Do not keep sync-over-async registration in the runtime.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: this is the core runtime migration where Mimir semantics must be preserved while swapping the underlying engine.
  - **Skills**: `[]`
  - **Skills Evaluated but Omitted**:
    - `fullstack-dev`: not necessary because this is backend runtime adaptation, not a cross-stack feature build.
    - `playwright`: no browser path involved.

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 5, 6)
  - **Blocks**: 7, 8, 10
  - **Blocked By**: 1, 2

  **References**:
  - `src/nem.Mimir.Infrastructure/Plugins/PluginManager.cs` - current bespoke runtime that owns reflection load, activation, execution, and sync built-in registration.
  - `src/nem.Mimir.Application/Common/Interfaces/IPluginService.cs` - façade whose method signatures and semantics must remain stable.
  - `/workspace/wmreflect/nem.Plugins.Sdk/src/nem.Plugins.Sdk.Loader/PluginManager.cs` - SDK lifecycle manager pattern for initialize/start/stop/dispose and isolated plugin failures.
  - `/workspace/wmreflect/nem.Plugins.Sdk/src/nem.Plugins.Sdk.Loader/ServiceCollectionExtensions.cs` - DI registration pattern for the SDK plugin subsystem.
  - `src/nem.Mimir.Domain/Plugins/PluginMetadata.cs` - metadata shape that must continue being returned by Mimir APIs/application handlers.
  - `src/nem.Mimir.Domain/Plugins/PluginResult.cs` - execution result semantics that the adapter must continue producing.
  - `tests/nem.Mimir.Infrastructure.Tests/Plugins/PluginManagerTests.cs` - existing positive runtime coverage to preserve and extend during migration.
  - `tests/nem.Mimir.Infrastructure.Tests/Plugins/PluginManagerNegativeTests.cs` - negative-path regression suite for host safety and duplicate plugin protection.

  **Acceptance Criteria**:
  - [ ] `IPluginService` still exposes the same four operations while runtime internals are SDK-backed.
  - [ ] No runtime path depends on `RegisterPlugin(...).GetAwaiter().GetResult()`.
  - [ ] Plugin execution failures still map to `PluginResult.Failure(...)` where current behavior expects graceful failure.
  - [ ] `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter PluginManager` passes after migration.

  **QA Scenarios**:
  ```text
  Scenario: SDK-backed runtime still passes positive plugin manager flow tests
    Tool: Bash (dotnet test)
    Preconditions: Task 4 implementation is complete in working tree
    Steps:
      1. Run `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter PluginManagerTests`.
      2. Confirm load/list/execute/unload coverage passes against the migrated runtime.
      3. Save output to `.sisyphus/evidence/task-4-pluginmanager-tests.txt`.
    Expected Result: positive runtime behavior remains green while internals use the SDK-backed adapter.
    Failure Indicators: broken list/load/execute/unload behavior, duplicate registration regressions, or compile failures in runtime tests.
    Evidence: .sisyphus/evidence/task-4-pluginmanager-tests.txt

  Scenario: Runtime no longer uses sync-over-async built-in registration
    Tool: Bash (grep)
    Preconditions: Task 4 implementation is complete in working tree
    Steps:
      1. Run `grep -R "GetAwaiter().GetResult" -n src/nem.Mimir.Infrastructure --include="*.cs"`.
      2. Save output to `.sisyphus/evidence/task-4-sync-over-async-grep.txt`.
      3. Verify no plugin runtime registration path still relies on sync-over-async blocking.
    Expected Result: no remaining sync-over-async registration usage in plugin runtime code.
    Failure Indicators: any remaining `GetAwaiter().GetResult()` in migrated plugin runtime paths.
    Evidence: .sisyphus/evidence/task-4-sync-over-async-grep.txt
  ```

  **Evidence to Capture**:
  - [ ] `.sisyphus/evidence/task-4-pluginmanager-tests.txt`
  - [ ] `.sisyphus/evidence/task-4-sync-over-async-grep.txt`

  **Commit**: YES
  - Message: `refactor(plugins): migrate mimir runtime to sdk-backed adapter`
  - Files: `src/nem.Mimir.Infrastructure/Plugins/PluginManager.cs`, related runtime tests
  - Pre-commit: `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter PluginManager`

- [ ] 5. Migrate DI-backed built-in plugins to a common contribution contract

  **What to do**:
  - Replace the concrete-cast `BuiltInPluginRegistrar` pattern with a contribution/catalog mechanism that can register DI-backed built-ins through the same runtime seam as external plugins.
  - Bring `CodeRunnerPlugin`, `WebSearchPlugin`, and `SkillMarketplacePlugin` onto the new SDK-derived Mimir plugin seam and ensure all three are discovered/registered consistently.
  - Update DI wiring so built-in registration is explicit, async-safe, and does not depend on `Activator.CreateInstance`.
  - Ensure the missing `SkillMarketplacePlugin` is actually included in startup registration after the migration.

  **Must NOT do**:
  - Do not cast `IPluginService` back to `PluginManager`.
  - Do not instantiate DI-backed built-ins through reflection-only activation.
  - Do not drop any of the built-in plugins currently expected in scope.

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: focused infrastructure migration across several concrete built-in plugins and DI wiring.
  - **Skills**: `[]`
  - **Skills Evaluated but Omitted**:
    - `fullstack-dev`: this is runtime/service registration work, not a full-stack implementation.
    - `playwright`: no UI interaction required.

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 4, 6)
  - **Blocks**: 7, 8, 10
  - **Blocked By**: 1

  **References**:
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/BuiltInPluginRegistrar.cs` - current anti-pattern that casts `IPluginService` to concrete `PluginManager`.
  - `src/nem.Mimir.Infrastructure/DependencyInjection.cs` - current singleton registrations and hosted service wiring for built-ins.
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/CodeRunnerPlugin.cs` - DI-backed built-in plugin using `IServiceScopeFactory`.
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/WebSearchPlugin.cs` - built-in plugin that must preserve error-handling and search semantics.
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/SkillMarketplacePlugin.cs` - real built-in plugin currently missing from registrar startup wiring.
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Plugins/WorkflowPluginLoader.cs` - hosted-loader pattern to emulate instead of ad hoc registrar casting.
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Plugins/WorkflowPluginCatalog.cs` - example host-side catalog for runtime registrations.
  - `tests/nem.Mimir.Infrastructure.Tests/Plugins/BuiltIn/CodeRunnerPluginTests.cs` - expected behavior that must remain green after migration.
  - `tests/nem.Mimir.Infrastructure.Tests/Plugins/BuiltIn/WebSearchPluginTests.cs` - expected built-in behavior for search plugin.

  **Acceptance Criteria**:
  - [ ] Built-in plugins register through a common contribution/catalog path without concrete `PluginManager` casts.
  - [ ] `CodeRunnerPlugin`, `WebSearchPlugin`, and `SkillMarketplacePlugin` all flow through the same runtime seam.
  - [ ] DI-backed built-ins are not created via `Activator.CreateInstance`.
  - [ ] Built-in plugin tests continue passing after migration.

  **QA Scenarios**:
  ```text
  Scenario: Built-in plugin unit tests stay green after registration-model migration
    Tool: Bash (dotnet test)
    Preconditions: Task 5 implementation is complete in working tree
    Steps:
      1. Run `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter "CodeRunnerPluginTests|WebSearchPluginTests"`.
      2. Confirm both suites pass with no DI or initialization regressions.
      3. Save output to `.sisyphus/evidence/task-5-builtins-tests.txt`.
    Expected Result: built-in plugin behavior remains correct after moving to the common contribution seam.
    Failure Indicators: DI resolution failures, changed failure messages, or broken plugin initialization behavior.
    Evidence: .sisyphus/evidence/task-5-builtins-tests.txt

  Scenario: Startup wiring includes marketplace plugin and removes concrete cast pattern
    Tool: Bash (grep)
    Preconditions: Task 5 implementation is complete in working tree
    Steps:
      1. Run `grep -R "SkillMarketplacePlugin\|BuiltInPluginRegistrar\|PluginManager)pluginService" -n src/nem.Mimir.Infrastructure --include="*.cs"`.
      2. Save output to `.sisyphus/evidence/task-5-builtin-registration-grep.txt`.
      3. Verify `SkillMarketplacePlugin` is present in the new registration path and the concrete cast anti-pattern is gone.
    Expected Result: marketplace plugin is registered and concrete `PluginManager` cast usage has been eliminated.
    Failure Indicators: missing marketplace registration or continued `IPluginService`→`PluginManager` cast.
    Evidence: .sisyphus/evidence/task-5-builtin-registration-grep.txt
  ```

  **Evidence to Capture**:
  - [ ] `.sisyphus/evidence/task-5-builtins-tests.txt`
  - [ ] `.sisyphus/evidence/task-5-builtin-registration-grep.txt`

  **Commit**: YES
  - Message: `refactor(plugins): unify built-in plugin registration`
  - Files: built-in plugins, DI wiring, hosted loader/catalog classes
  - Pre-commit: `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter "CodeRunnerPluginTests|WebSearchPluginTests"`

- [ ] 6. Migrate finance plugin package and plugin metadata flow

  **What to do**:
  - Move `FinanceToolRegistryPlugin` onto the new SDK-derived Mimir plugin seam while preserving its current execution payload contract (`server=finance`, `tools=[...]`).
  - Ensure the separate `nem.Mimir.Finance.McpTools` project participates in the same built-in contribution/runtime flow as other built-ins.
  - Verify plugin metadata/identity for the finance plugin still surfaces correctly through list/load behavior after migration.

  **Must NOT do**:
  - Do not break the current tool payload schema returned by the finance plugin.
  - Do not leave the finance package on the old bespoke-only plugin contract.
  - Do not couple finance-plugin registration to special-case runtime logic that other built-ins do not need.

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: scoped cross-project plugin contract migration with a tight existing test suite.
  - **Skills**: `[]`
  - **Skills Evaluated but Omitted**:
    - `fullstack-dev`: not required; this is a focused package/runtime integration task.
    - `playwright`: no browser path involved.

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 4, 5)
  - **Blocks**: 7, 10
  - **Blocked By**: 1

  **References**:
  - `src/nem.Mimir.Finance.McpTools/FinanceToolRegistryPlugin.cs` - finance plugin implementation that must adopt the new seam without changing payload behavior.
  - `tests/nem.Mimir.Finance.McpTools.Tests/FinanceToolRegistryPluginTests.cs` - existing assertions for payload shape, lifecycle behavior, and cancellation.
  - `src/nem.Mimir.Infrastructure/DependencyInjection.cs` - current finance plugin DI registration.
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/BuiltInPluginRegistrar.cs` - current finance plugin registration path to replace.
  - `src/nem.Mimir.Domain/Plugins/PluginMetadata.cs` - list/load metadata contract that must remain stable for the finance plugin.

  **Acceptance Criteria**:
  - [ ] `FinanceToolRegistryPlugin` implements the SDK-derived Mimir seam.
  - [ ] Existing finance plugin tests remain green.
  - [ ] Finance plugin registration goes through the common built-in contribution/runtime path.
  - [ ] Finance plugin metadata remains visible through plugin listing behavior.

  **QA Scenarios**:
  ```text
  Scenario: Finance plugin package tests stay green after seam migration
    Tool: Bash (dotnet test)
    Preconditions: Task 6 implementation is complete in working tree
    Steps:
      1. Run `dotnet test tests/nem.Mimir.Finance.McpTools.Tests`.
      2. Confirm payload and lifecycle tests pass without contract regressions.
      3. Save output to `.sisyphus/evidence/task-6-finance-plugin-tests.txt`.
    Expected Result: finance plugin behavior remains unchanged while moving onto the shared plugin seam.
    Failure Indicators: changed payload structure, broken lifecycle semantics, or compile failures from contract changes.
    Evidence: .sisyphus/evidence/task-6-finance-plugin-tests.txt

  Scenario: Finance plugin still exposes expected server/tools metadata contract
    Tool: Bash (grep)
    Preconditions: Task 6 implementation is complete in working tree
    Steps:
      1. Run `grep -n "\[\"server\"\]\|\[\"tools\"\]\|Id =>" src/nem.Mimir.Finance.McpTools/FinanceToolRegistryPlugin.cs`.
      2. Save output to `.sisyphus/evidence/task-6-finance-plugin-grep.txt`.
      3. Verify the finance plugin still declares stable identity and the `server/tools` payload keys.
    Expected Result: plugin identity and returned payload shape remain explicit and unchanged.
    Failure Indicators: missing `server`/`tools` keys, changed identity, or accidental schema drift.
    Evidence: .sisyphus/evidence/task-6-finance-plugin-grep.txt
  ```

  **Evidence to Capture**:
  - [ ] `.sisyphus/evidence/task-6-finance-plugin-tests.txt`
  - [ ] `.sisyphus/evidence/task-6-finance-plugin-grep.txt`

  **Commit**: YES
  - Message: `refactor(plugins): migrate finance registry plugin`
  - Files: `src/nem.Mimir.Finance.McpTools/*`, related infrastructure wiring
  - Pre-commit: `dotnet test tests/nem.Mimir.Finance.McpTools.Tests`

- [ ] 7. Replace startup wiring with a hosted loader and plugin catalog pattern

  **What to do**:
  - Replace `BuiltInPluginRegistrar` startup behavior with a hosted loader/catalog arrangement that follows the workspace plugin-host pattern and works for both built-ins and externally loaded plugins.
  - Move plugin startup orchestration out of ad hoc DI registration into a dedicated infrastructure component that initializes, starts, and stops the plugin subsystem safely.
  - Introduce or adapt a host-side plugin catalog so Mimir can track registered plugin capabilities/instances without concrete `PluginManager` coupling.
  - Update `DependencyInjection.cs` so startup wiring reflects the new hosted loader path instead of `AddHostedService<BuiltInPluginRegistrar>()`.

  **Must NOT do**:
  - Do not allow plugin subsystem startup/shutdown failures to crash the host.
  - Do not keep both the old registrar and the new hosted loader active at the same time.
  - Do not bypass the runtime/catalog seam introduced in Wave 2.

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: this is host-lifecycle orchestration touching startup, shutdown, DI wiring, and runtime ownership boundaries.
  - **Skills**: `[]`
  - **Skills Evaluated but Omitted**:
    - `fullstack-dev`: no frontend or external API expansion is needed.
    - `playwright`: no browser path involved.

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 8, 9, 10)
  - **Blocks**: 8, 9, 10
  - **Blocked By**: 1, 2, 4, 5, 6

  **References**:
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/BuiltInPluginRegistrar.cs` - old hosted registration model to remove/replace.
  - `src/nem.Mimir.Infrastructure/DependencyInjection.cs` - current `AddSingleton<IPluginService, PluginManager>()` and `AddHostedService<BuiltInPluginRegistrar>()` startup seam.
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Plugins/WorkflowPluginLoader.cs` - canonical hosted-loader lifecycle pattern with safe initialize/start/stop/dispose behavior.
  - `/workspace/wmreflect/nem.Workflow/src/nem.Workflow.Infrastructure/Plugins/WorkflowPluginCatalog.cs` - example runtime catalog structure for host-owned plugin registrations.
  - `/workspace/wmreflect/nem.Plugins.Sdk/src/nem.Plugins.Sdk.Loader/PluginManager.cs` - SDK manager lifecycle surface that the hosted loader should orchestrate.

  **Acceptance Criteria**:
  - [ ] `BuiltInPluginRegistrar` is replaced or retired in favor of a hosted loader/catalog startup path.
  - [ ] Plugin subsystem startup and shutdown are isolated behind hosted-service orchestration.
  - [ ] `DependencyInjection.cs` no longer wires the old registrar path.
  - [ ] Host startup wiring still preserves Mimir plugin availability without concrete `PluginManager` casts.

  **QA Scenarios**:
  ```text
  Scenario: Infrastructure startup wiring uses hosted loader pattern instead of old registrar
    Tool: Bash (grep)
    Preconditions: Task 7 implementation is complete in working tree
    Steps:
      1. Run `grep -R "BuiltInPluginRegistrar\|AddHostedService<\|AddSingleton<IPluginService" -n src/nem.Mimir.Infrastructure --include="*.cs"`.
      2. Save output to `.sisyphus/evidence/task-7-hosted-loader-grep.txt`.
      3. Verify the old registrar path is gone or retired and the new hosted loader wiring is present.
    Expected Result: startup code references the new hosted loader/catalog path rather than the old registrar cast pattern.
    Failure Indicators: `BuiltInPluginRegistrar` still active in DI or no hosted loader replacement present.
    Evidence: .sisyphus/evidence/task-7-hosted-loader-grep.txt

  Scenario: Full solution still builds after startup orchestration migration
    Tool: Bash (dotnet build)
    Preconditions: Task 7 implementation is complete in working tree
    Steps:
      1. Run `dotnet build nem.Mimir.slnx --warnaserror`.
      2. Confirm infrastructure startup changes compile cleanly.
      3. Save output to `.sisyphus/evidence/task-7-build.txt`.
    Expected Result: the repo builds successfully with the new hosted loader/catalog wiring.
    Failure Indicators: compile failures in DI wiring, hosted service registration, or plugin catalog classes.
    Evidence: .sisyphus/evidence/task-7-build.txt
  ```

  **Evidence to Capture**:
  - [ ] `.sisyphus/evidence/task-7-hosted-loader-grep.txt`
  - [ ] `.sisyphus/evidence/task-7-build.txt`

  **Commit**: YES
  - Message: `refactor(plugins): add hosted plugin loader and catalog`
  - Files: infrastructure plugin loader/catalog/wiring files
  - Pre-commit: `dotnet build nem.Mimir.slnx --warnaserror`

- [ ] 8. Preserve controller, command, and end-to-end plugin operation flow

  **What to do**:
  - Verify and preserve the full `PluginsController` → application command/query handler → `IPluginService` flow after startup/runtime migration.
  - Add or tighten end-to-end-ish tests that prove load/list/execute/unload behavior still works from the application/controller boundary.
  - Ensure plugin metadata and execution payloads produced by built-ins and migrated runtime paths still align with the REST contract.

  **Must NOT do**:
  - Do not redesign routes, command DTOs, or controller action names.
  - Do not convert the flow to a different messaging or controller architecture.
  - Do not accept regressions hidden only by lower-level unit tests.

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: this is integration preservation work spanning controller, handlers, and runtime seams without introducing a new architecture.
  - **Skills**: `[]`
  - **Skills Evaluated but Omitted**:
    - `playwright`: the critical verification is backend/API contract, not browser UI.
    - `fullstack-dev`: no broader full-stack scope change is required.

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 7, 9, 10)
  - **Blocks**: 10
  - **Blocked By**: 3, 4, 5, 7

  **References**:
  - `src/nem.Mimir.Api/Controllers/PluginsController.cs` - REST contract and controller action wiring that must remain unchanged.
  - `src/nem.Mimir.Application/Plugins/Commands/LoadPlugin.cs` - load command boundary over `IPluginService`.
  - `src/nem.Mimir.Application/Plugins/Commands/ExecutePlugin.cs` - execute command boundary and `PluginContext.Create(...)` flow.
  - `src/nem.Mimir.Application/Plugins/Commands/UnloadPlugin.cs` - unload command boundary.
  - `src/nem.Mimir.Application/Plugins/Queries/ListPlugins.cs` - list query boundary.
  - `src/nem.Mimir.Application/Common/Interfaces/IPluginService.cs` - stable façade contract being exercised end-to-end.
  - `src/nem.Mimir.Domain/Plugins/PluginMetadata.cs` - list/load payload shape.
  - `src/nem.Mimir.Domain/Plugins/PluginResult.cs` - execute payload/result shape.
  - `tests/nem.Mimir.Infrastructure.Tests/Plugins/PluginManagerTests.cs` - current runtime behavior tests that can be extended to cover the integrated flow.
  - `tests/nem.Mimir.Infrastructure.Tests/Plugins/PluginManagerNegativeTests.cs` - edge/failure conditions that must remain correct after integration changes.

  **Acceptance Criteria**:
  - [ ] Controller/app-layer load/list/execute/unload semantics remain stable after runtime and startup changes.
  - [ ] Regression coverage exists for the integrated plugin operation flow, not just isolated runtime units.
  - [ ] Metadata/result payloads still match the existing contract.
  - [ ] Plugin-related infrastructure test suites remain green.

  **QA Scenarios**:
  ```text
  Scenario: Integrated plugin operation suites stay green after runtime/startup migration
    Tool: Bash (dotnet test)
    Preconditions: Task 8 implementation is complete in working tree
    Steps:
      1. Run `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter Plugin`.
      2. Confirm load/list/execute/unload regressions remain green across positive and negative suites.
      3. Save output to `.sisyphus/evidence/task-8-plugin-integration-tests.txt`.
    Expected Result: plugin operation behavior remains intact from the controller/application seam down into the runtime.
    Failure Indicators: broken load/list/execute/unload expectations, payload drift, or failing plugin integration tests.
    Evidence: .sisyphus/evidence/task-8-plugin-integration-tests.txt

  Scenario: Controller route and dispatch shape remains unchanged
    Tool: Bash (grep)
    Preconditions: Task 8 implementation is complete in working tree
    Steps:
      1. Run `grep -n "\[HttpPost\]\|\[HttpGet\]\|\[HttpDelete\|InvokeAsync" src/nem.Mimir.Api/Controllers/PluginsController.cs`.
      2. Save output to `.sisyphus/evidence/task-8-controller-flow-grep.txt`.
      3. Verify the controller still exposes the same route/dispatch shape.
    Expected Result: the REST/controller boundary is unchanged while internals are migrated.
    Failure Indicators: route drift, removed endpoint, or changed dispatch style.
    Evidence: .sisyphus/evidence/task-8-controller-flow-grep.txt
  ```

  **Evidence to Capture**:
  - [ ] `.sisyphus/evidence/task-8-plugin-integration-tests.txt`
  - [ ] `.sisyphus/evidence/task-8-controller-flow-grep.txt`

  **Commit**: YES
  - Message: `test(plugins): preserve end-to-end plugin operation flow`
  - Files: controller/application/runtime regression tests
  - Pre-commit: `dotnet test tests/nem.Mimir.Infrastructure.Tests --filter Plugin`

- [ ] 9. Remove placeholder and TODO-backed active-surface behavior

  **What to do**:
  - Replace the `// TODO: switch to tokens` placeholder in `src/mimir-chat/pages/api/google.ts` with real behavior that enforces a deliberate truncation/token-budget strategy.
  - Ensure the active plugin-adjacent source surface has no unresolved `TODO`, `FIXME`, `HACK`, stub, or fake behavior left in touched files.
  - Add or update frontend/API-route tests if needed so the replacement behavior is executable and not just commented intent.

  **Must NOT do**:
  - Do not leave the old comment in place.
  - Do not replace the comment with another placeholder or magic constant with no rationale.
  - Do not broaden this into unrelated frontend redesign.

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: focused cleanup of a confirmed active placeholder with bounded blast radius.
  - **Skills**: `[]`
  - **Skills Evaluated but Omitted**:
    - `frontend-dev`: this is not a UI redesign task; it is a narrow API-route logic cleanup.
    - `playwright`: route behavior can be verified via build/test/static checks without browser automation.

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 7, 8, 10)
  - **Blocks**: 10
  - **Blocked By**: 7

  **References**:
  - `src/mimir-chat/pages/api/google.ts` - confirmed active placeholder location at line 71 (`TODO: switch to tokens`).
  - `src/mimir-chat/package.json` - authoritative frontend commands (`build`, `lint`, `test=vitest`) to use for verification.
  - `src/mimir-chat/types/google.ts` - response/request shape reference if truncation behavior or payload contracts need to stay aligned.
  - `src/mimir-chat/utils/server/google.ts` - existing source-cleaning logic likely related to truncation behavior.

  **Acceptance Criteria**:
  - [ ] The active `google.ts` placeholder comment is removed and replaced by real logic.
  - [ ] No unresolved `TODO|FIXME|HACK` markers remain in touched plugin-related and active frontend route files.
  - [ ] Frontend route code still builds/tests cleanly after the change.
  - [ ] The truncation/token-budget behavior is explicit in code, not implied by comments.

  **QA Scenarios**:
  ```text
  Scenario: Frontend route still builds/tests after replacing placeholder logic
    Tool: Bash (npm)
    Preconditions: Task 9 implementation is complete in working tree
    Steps:
      1. Run `npm run build` in `src/mimir-chat`.
      2. Save output to `.sisyphus/evidence/task-9-mimir-chat-build-or-test.txt`.
      3. Confirm the route code compiles with the placeholder removed.
    Expected Result: `mimir-chat` route code builds/tests successfully with real truncation logic.
    Failure Indicators: TypeScript build failures, route test failures, or placeholder-dependent code paths.
    Evidence: .sisyphus/evidence/task-9-mimir-chat-build-or-test.txt

  Scenario: No unresolved placeholder markers remain in active target surface
    Tool: Bash (grep)
    Preconditions: Task 9 implementation is complete in working tree
    Steps:
      1. Run `grep -R "TODO\|FIXME\|HACK" src/nem.Mimir.Infrastructure src/nem.Mimir.Domain src/nem.Mimir.Application src/mimir-chat/pages/api --include="*.cs" --include="*.ts" --include="*.tsx"`.
      2. Save output to `.sisyphus/evidence/task-9-placeholder-grep.txt`.
      3. Verify the previous `google.ts` TODO is gone and no new unresolved markers were introduced in touched files.
    Expected Result: no unresolved placeholder markers remain in the active target surface covered by this refactor.
    Failure Indicators: any remaining `TODO`, `FIXME`, or `HACK` in the scoped paths.
    Evidence: .sisyphus/evidence/task-9-placeholder-grep.txt
  ```

  **Evidence to Capture**:
  - [ ] `.sisyphus/evidence/task-9-mimir-chat-build-or-test.txt`
  - [ ] `.sisyphus/evidence/task-9-placeholder-grep.txt`

  **Commit**: YES
  - Message: `fix(plugins): remove placeholder-backed route behavior`
  - Files: `src/mimir-chat/pages/api/google.ts`, related frontend tests/helpers
  - Pre-commit: `npm run build`

- [ ] 10. Final regression hardening and dead-code cleanup

  **What to do**:
  - Remove obsolete compatibility shims, unused registrar/runtime leftovers, and dead code that the new plugin architecture makes unnecessary.
  - Harden regression coverage so the final architecture is protected against reintroducing the old anti-patterns (concrete casts, sync-over-async, unresolved placeholders, duplicate plugin seams).
  - Run full solution build/test validation and reconcile any plugin-related failures introduced by the refactor.

  **Must NOT do**:
  - Do not delete code that still backs preserved API behavior.
  - Do not leave dead compatibility classes or commented-out fallback paths behind.
  - Do not finish the refactor with failing solution-wide build/test status.

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: final cross-cutting cleanup and regression hardening across all touched runtime, application, and frontend surfaces.
  - **Skills**: `[]`
  - **Skills Evaluated but Omitted**:
    - `fullstack-dev`: not needed; this is stabilization/cleanup, not new full-stack feature work.
    - `playwright`: final required verification is solution build/test plus grep/evidence, not UI browsing.

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 7, 8, 9)
  - **Blocks**: F1, F2, F3, F4
  - **Blocked By**: 2, 3, 4, 5, 6, 7, 8, 9

  **References**:
  - `src/nem.Mimir.Infrastructure/Plugins/PluginManager.cs` - likely source of dead runtime paths after SDK migration.
  - `src/nem.Mimir.Infrastructure/Plugins/BuiltIn/BuiltInPluginRegistrar.cs` - expected deletion/retirement target if replaced by hosted loader.
  - `src/nem.Mimir.Infrastructure/DependencyInjection.cs` - final registration cleanup point.
  - `tests/nem.Mimir.Infrastructure.Tests/Plugins/PluginManagerTests.cs` - baseline runtime suite to keep green.
  - `tests/nem.Mimir.Infrastructure.Tests/Plugins/PluginManagerNegativeTests.cs` - anti-regression safety net for failure paths.
  - `tests/nem.Mimir.Finance.McpTools.Tests/FinanceToolRegistryPluginTests.cs` - finance package regression coverage.
  - `src/mimir-chat/package.json` - frontend verification commands for the placeholder-removal path.

  **Acceptance Criteria**:
  - [ ] Obsolete runtime/registrar leftovers are removed or formally retired.
  - [ ] Full solution build passes with warnings treated as errors.
  - [ ] Full solution test run passes.
  - [ ] No plugin-related anti-patterns targeted by this refactor remain in touched code.

  **QA Scenarios**:
  ```text
  Scenario: Full solution build passes after final cleanup
    Tool: Bash (dotnet build)
    Preconditions: Tasks 1-9 are complete and cleanup is applied
    Steps:
      1. Run `dotnet build nem.Mimir.slnx --warnaserror`.
      2. Save output to `.sisyphus/evidence/task-10-build.txt`.
      3. Confirm there are no warnings promoted to errors and no plugin-related compile failures.
    Expected Result: the full solution builds cleanly after the refactor.
    Failure Indicators: any build failure or warning-as-error regression in touched projects.
    Evidence: .sisyphus/evidence/task-10-build.txt

  Scenario: Full solution test suite passes after final cleanup
    Tool: Bash (dotnet test)
    Preconditions: Tasks 1-9 are complete and cleanup is applied
    Steps:
      1. Run `dotnet test nem.Mimir.slnx`.
      2. Save output to `.sisyphus/evidence/task-10-test.txt`.
      3. Confirm plugin runtime, built-in plugin, finance plugin, and related regression suites all pass.
    Expected Result: the solution test suite passes with the new plugin architecture in place.
    Failure Indicators: failing plugin-related tests, runtime regressions, or unresolved migration breakages.
    Evidence: .sisyphus/evidence/task-10-test.txt
  ```

  **Evidence to Capture**:
  - [ ] `.sisyphus/evidence/task-10-build.txt`
  - [ ] `.sisyphus/evidence/task-10-test.txt`

  **Commit**: YES
  - Message: `test(plugins): harden regressions and remove dead plugin runtime paths`
  - Files: all remaining touched runtime/application/frontend cleanup files
  - Pre-commit: `dotnet build nem.Mimir.slnx --warnaserror && dotnet test nem.Mimir.slnx`

---

## Final Verification Wave

> 4 review agents run in parallel after all implementation tasks complete. All must approve before work is presented as done.

- [ ] F1. **Plan Compliance Audit** — `oracle`
  Verify every deliverable, must-have item, and must-not-have guardrail against the final implementation and evidence.

- [ ] F2. **Code Quality Review** — `unspecified-high`
  Run build/tests, scan for anti-patterns (`as any`, empty catches, TODOs, dead compatibility code), and verify touched files remain warning-free.

- [ ] F3. **Real QA Execution** — `unspecified-high`
  Execute every task QA scenario, save evidence under `.sisyphus/evidence/final-qa/`, and verify cross-task integration.

- [ ] F4. **Scope Fidelity Check** — `deep`
  Compare delivered changes against this plan to ensure no missing required work and no out-of-scope architecture drift.

---

## Commit Strategy

- **Wave 1**: `refactor(plugins): establish sdk-aligned mimir plugin seam`
- **Wave 2**: `refactor(plugins): migrate runtime and built-in plugin registration`
- **Wave 3**: `refactor(plugins): wire hosted plugin catalog and remove placeholder debt`
- **Final cleanup**: `test(plugins): harden regression coverage and remove legacy plugin runtime leftovers`

---

## Success Criteria

### Verification Commands
```bash
dotnet build nem.Mimir.slnx --warnaserror
dotnet test nem.Mimir.slnx
grep -R "TODO\|FIXME\|HACK" src/nem.Mimir.Infrastructure src/nem.Mimir.Domain src/nem.Mimir.Application src/mimir-chat/pages/api --include="*.cs" --include="*.ts" --include="*.tsx"
```

### Final Checklist
- [ ] All must-have items are present
- [ ] All must-not-have items are absent
- [ ] Plugin API behavior is preserved
- [ ] Built-in plugins flow through the common runtime/catalog seam
- [ ] ALC isolation + shared contract identity are preserved
- [ ] Tests and build pass cleanly
