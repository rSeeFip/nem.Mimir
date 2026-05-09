# Decisions

## 2026-04-09 Session Start
- No worktree — working directly in repos
- Plan spans 3 repos: nem.Mimir-typed-ids, nem.Workflow, nem.Sentinel
- Wave 1 tasks (T1-T5) are fully parallel
- Commit per task per repo touched, tagged with [TN]

## Architecture Decisions
- Plan/provider seam pattern for Mimir orchestration externalization
- IOrchestrationStrategy + IStepTypeRegistry as Workflow extension points
- Loader-backed playbook definitions for Sentinel
- Workflow-backed executor for Sentinel remediation
- All safety gates (AutonomyManager, L3SafetyGuard) preserved as interfaces/adapters

## 2026-04-10 F1 Re-Audit
- For the compliance re-run, evidence scoring followed the orchestrator's `17+ for T1-T14` threshold instead of the original 28 artifact plan-level enumeration.
- Approved F2/F3 reports were accepted as the authoritative build/test baseline; F1 re-ran only guardrails, evidence existence, and targeted symbol checks.
