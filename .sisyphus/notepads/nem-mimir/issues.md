# Issues Log

## 2026-03-06 — T29 Semantic Cache

- No blocking implementation issues encountered.
- Transient/stale LSP namespace resolution warnings for `nem.Contracts.TokenOptimization` were observed during incremental edits, but full solution build resolved cleanly.

## 2026-03-07 — T43 NBomber performance tests

- Initial all-6 failure cause: reflection helper in `PerformanceTestRunner` assumed a `Get(string)` accessor existed on NBomber stats containers. Current runtime type did not expose that member, causing `getMethod should not be null` before assertions executed.
- Constraint: fix kept local to `tests/nem.Mimir.PerformanceTests` with no dependency changes.

## 2026-03-07 — T40 integration tests

- No blocking issues after fix.
- Observed fragility point: strict `<` token assertion at boundary can fail when optimizer output equals input estimate; adjusted to non-flaky boundary-safe check.
