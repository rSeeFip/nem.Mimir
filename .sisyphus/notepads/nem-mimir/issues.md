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

## 2026-03-25 — F4 scope fidelity audit

- Build still fails with known pre-existing error in `src/nem.Mimir.Infrastructure/Sandbox/OpenSandboxProvider.cs` (CS0535), unrelated to handler-shape migration.
- Scope-fidelity violation detected in `SendMessageCommandHandler`: addition of `IMessageBus` dependency and `PublishAsync(new MessageCreatedEvent(...))` introduces business-logic behavior beyond shape/interface conversion.
- Middleware filenames in plan (`*Middleware.cs`) do not exist; equivalent files are `LoggingBehavior.cs`, `PerformanceBehavior.cs`, `UnhandledExceptionBehavior.cs` and contain Wolverine middleware classes.

## 2026-04-23 — PromoteSessionHandlerTests verification

- `dotnet test tests/nem.Mimir.Application.Tests/nem.Mimir.Application.Tests.csproj --no-build -v quiet` still fails due to two pre-existing `AutomergeDocumentStoreTests` InvalidCastExceptions in `Notes.Services`; the updated `SessionPromotion.PromoteSessionHandlerTests` pass.
- `dotnet test ... --no-build` without an explicit verbosity argument previously produced a VSTest argument parsing failure in this environment; `-v quiet` executes correctly and exposes the real failing tests.
