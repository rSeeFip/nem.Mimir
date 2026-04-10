# Issues

## Pre-existing Issues (not our responsibility)
- mimir-chat React components have pre-existing TSLint/hook errors in ChatMessage.tsx, Chat.tsx, ChatInput.tsx
- globals.css has tailwind at-rule warnings
- These were present before our work began

## Known TODOs to fix (per plan)
- `src/mimir-chat/pages/api/google.ts` line 71: `TODO: switch to tokens` — must be fixed in T13

## T10 verification status update (2026-04-10 08:42:27Z)
- `RemediationExecutorTests` now pass after fixing the workflow-backed test fixture mismatch.
- Broader `MapekLoopTests` host startup failures remain separate from this fix and were not changed in this step.

## T10 MapekLoop status (2026-04-10 11:54:54Z)
- Host startup remains fixed and `MapekLoopTests` now pass 3/3 after aligning test autonomy setup with loader-backed playbooks.
- Baseline warnings remain: `NU1903` on `System.Linq.Dynamic.Core` in `nem.Sentinel.Infrastructure.Tests`; no new functional blockers from this fix.

## F4 scope fidelity blockers (2026-04-10T00:00:00Z)
- Missing `[T8]`-`[T14]` task-tagged commits across the three repos prevents clean per-task diff isolation required by the plan's F4 procedure.
- `nem.Workflow` contains untracked `.serena/` artifacts outside plan scope.
- `nem.Mimir-typed-ids` contains unrelated `.sisyphus` workspace artifacts and an unrelated deleted plan file in the working tree.
- Current Workflow/Mimir/Sentinel working trees mix later-wave tasks together, producing cross-task contamination and forcing F4 to reject the branch state.

## F1 compliance audit (2026-04-10)
- Final audit rejected the plan delivery: only 8/28 referenced evidence files were present, task-level acceptance criteria totaled 32/42, and final full-suite test evidence (`task-14-tests.txt`) was not clean.
- Remaining architectural gaps called out by F1: Mimir still uses a local plugin seam instead of SDK-derived contracts, Sentinel distributed-safety remains process-local (`CooldownManager`, `SemaphoreSlim` locks), and workflow-first fallback cleanup is incomplete in Mimir/Sentinel.
