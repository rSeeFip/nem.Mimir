---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Quality Assurance Documentation: nem.Mimir

## Quality strategy
The repository uses layered testing to validate domain invariants, application handlers, infrastructure adapters, API surfaces, and end-to-end behavior (including MCP tool pipelines).

## Test suite composition
Projects under `tests/` include:
- `nem.Mimir.Domain.Tests`
- `nem.Mimir.Application.Tests`
- `nem.Mimir.Infrastructure.Tests`
- `nem.Mimir.Api.Tests`
- `nem.Mimir.Api.IntegrationTests`
- `nem.Mimir.Sync.Tests`
- `nem.Mimir.Telegram.Tests`
- `nem.Mimir.Tui.Tests`
- `nem.Mimir.Integration.Tests`
- `nem.Mimir.E2E.Tests`

Observed test file inventory from repository scan is large (100+ test files), with specialized negative-test suites across layers.

## Test pyramid mapping
### Unit tests
- Domain entities/value objects/events.
- Application commands/queries/behaviors.
- Infrastructure services (sanitization, context window, LiteLLM serialization/tool calls).

### Integration tests
- API controllers against test host.
- repository behaviors against DB-backed contexts.
- cross-channel identity and channel integration fixtures.

### E2E tests
- Auth, conversations, chat completion.
- MCP tool pipeline and failure handling.
- health checks and negative-path request validation.

## High-value quality areas
- Prompt injection and malformed-input rejection.
- Conversation ownership isolation.
- Tool loop behavior and max-iteration boundaries.
- MCP connectivity failure and graceful degradation.
- SSE stream parsing and fallback behavior.

## Automation commands
- Build: `dotnet build nem.Mimir.slnx`
- Full tests: `dotnet test nem.Mimir.slnx`

No custom global runner is required; solution-level execution is canonical.

## CI quality gates (recommended baseline)
1. Build must succeed for all projects.
2. Unit + integration + E2E test pass.
3. Security-related negative tests pass (validation/sanitization/MCP authorization).
4. Docs regeneration should include placeholder scan for generated docs.

## Risk-based test matrix
| Risk | Impact | Mitigation suite |
| :--- | :--- | :--- |
| Invalid/malicious input reaches LLM/tooling | High | API negative tests + sanitizer tests |
| Tool execution bypasses governance | High | MCP negative + tool whitelist tests |
| Data isolation regression | High | conversation ownership tests + E2E user boundary tests |
| Streaming instability | Medium | ChatHub tests + SSE parser tests |
| Messaging regression | Medium | Sync publisher/handler tests |

## Observed strengths
- Extensive negative testing present across API, domain, infra, and MCP.
- Dedicated test coverage for tool-calling serialization and MCP failure modes.
- Separate test projects preserve architecture boundaries.

## Observed gaps to monitor
- Legacy branch drift against active typed-ID branch may invalidate some assumptions over time.
- Model/catalog drift requires regression checks around `ListModels` and selection defaults.

## Cross-references
- [BUSINESS-LOGIC](./BUSINESS-LOGIC.md)
- [AI](./AI.md)
- [SECURITY](./SECURITY.md)
- [INFRASTRUCTURE](./INFRASTRUCTURE.md)
