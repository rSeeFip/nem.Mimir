# mimir-phase5: Merge, Production Hardening, Test Stabilization & Frontend Polish

## TODOs

- [x] T1: Merge `feature/mcp-client-integration` into `feat/typed-id-adoption`
- [x] T2: Build verification and unit test pass on merged branch
- [x] T3: Wire AddNemObservability() and AddNemSecrets() in Program.cs
- [x] T4: Create Helm chart nem-mimir
- [x] T5: Stabilize integration tests and enable in CI pipeline
- [x] T6: Add OTEL collector service to Docker Compose
- [x] T7: Improve health check endpoints with readiness probes
- [x] T8: Fix plugin response bug in Chat.tsx
- [x] T9: Remove console.log/console.* from production frontend code
- [x] T10: Replace native confirm() dialogs with modal components
- [x] T11: Fix inconsistent API paths and final frontend build verification

## Final Verification Wave

- [x] F1: Code quality review
- [x] F2: Security review
- [x] F3: Test coverage review
- [x] F4: Architecture review

## Notes

- T1 completed: merge commit `2ef6bcc`, 271 conflicts resolved with `--ours` strategy
- Plan file was deleted during merge (other branch didn't have it) - recreated from memory
- Wave 2 (parallel after T2): T3, T4, T5, T6
- T7 after T3
- Wave 3 (parallel after T2): T8, T9, T10
- T11 after T8, T9, T10
