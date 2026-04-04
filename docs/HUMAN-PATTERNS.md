---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Human Patterns Documentation: nem.Mimir

## Team conventions in this repo
- Treat `nem.Mimir` as a **legacy maintenance branch**.
- Keep architecture decisions consistent with existing dual-stack pattern (MediatR + Wolverine).
- Prefer repository interfaces and command/query handlers over controller-fat logic.
- Keep validation in FluentValidation and domain invariants, not ad-hoc controller checks.

## Branching and change policy
- New feature-forward work should target `nem.Mimir-typed-ids`.
- Changes in this branch should be constrained to maintenance, stabilization, and documentation parity.
- Cross-repo dependency changes (KnowHub agents/contracts) should be reviewed for compatibility impact.

## Code review checklist
1. Does request handling preserve ownership and auth boundaries?
2. Are prompt and message inputs validated and sanitized?
3. Are tool calls gated through whitelist/audit decorators?
4. Are Wolverine messages still durable/inbox-outbox protected?
5. Does change preserve OpenAI-compatible behavior on `/v1/*`?
6. Are tests added/updated in relevant test projects?

## Onboarding sequence for engineers
Recommended reading order:
1. `src/nem.Mimir.Api/Program.cs`
2. `OpenAiCompatController` and `MessagesController`
3. `SendMessageCommand` and `ContextWindowService`
4. `LiteLlmClient` + LiteLLM DTO models
5. MCP stack (`McpClientManager`, `McpToolProvider`, controller)
6. Security/sanitization middleware and validators

## Development workflow
- Implement behavior in Application or Infrastructure first.
- Wire API endpoints after handler/service behavior is stable.
- Add unit tests and relevant integration/E2E tests.
- Verify `dotnet build` and `dotnet test` for impacted projects/solution.
- Keep docs synchronized with concrete code paths.

## AI-assisted development policy
Allowed:
- generating draft docs from verified code references,
- proposing test cases for validator and tool-loop edges,
- summarizing endpoint behavior from controller/handler source.

Disallowed:
- introducing undocumented model/provider assumptions,
- copying generated code without validation against existing architecture,
- bypassing security checks for faster implementation.

## Knowledge capture patterns
- Use ADRs for architecture pivots (existing legacy ADR history is still useful context).
- Keep operations and compliance claims evidence-backed and branch-specific.
- Record known divergences (e.g., model default drift, legacy vs active branch split) explicitly.

## Practical collaboration boundaries
- API, App, Domain, Infrastructure, Sync each have clear ownership boundaries.
- MCP admin/config changes require security reviewer attention.
- Changes touching authentication or sanitization require security-focused review.

## Continuous improvement focus
- Reduce config/model naming drift between code constants and LiteLLM config.
- Prioritize typed-ID migration work in active branch while preserving legacy stability.
- Keep negative-test coverage for injection/tool misuse scenarios high.

## Cross-references
- [ARCHITECTURE](./ARCHITECTURE.md)
- [AI](./AI.md)
- [QA](./QA.md)
- [SECURITY](./SECURITY.md)
