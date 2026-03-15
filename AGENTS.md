# nem.Mimir-typed-ids — Agent Notes

> **ACTIVE** Mimir. Multi-channel conversational AI: Telegram, Teams, Web, Console, MCP. LLM routing (LiteLLM), agent hierarchy, triple-store memory. **nem.Mimir (without `-typed-ids`) is LEGACY — do not develop there.**

## Purpose

Central AI/chat service for nem.* ecosystem. Routes conversations through channel adapters (Telegram, Teams, Web, Console, MCP) via a unified MediatR CQRS layer. Manages LLM provider abstraction, agent orchestration with tool integration, and conversation memory.

## Status

- **nem.Mimir-typed-ids** (this repo): ✓ ACTIVE. Refactored with typed IDs from nem.Contracts. Current development branch.
- **nem.Mimir**: LEGACY. Pre-typed-IDs version. Superseded by nem.Mimir-typed-ids. Do not develop here.

## Structure

| Layer | Projects | Notes |
|-------|----------|-------|
| Core | Api, Application, Domain, Infrastructure | Clean Architecture + MediatR CQRS |
| Adapters | Adapter.{Telegram, Teams, Web, Console, MCP}, Sync | Channel isolation + cross-sync |
| Client | Tui, mimir-chat | Terminal UI + Next.js web |

For detailed project descriptions, see `src/AGENTS.md`.

## Key Patterns

- **MediatR CQRS**: NOT Wolverine (that's MCP/KnowHub). Never mix.
- **Typed IDs**: All entity IDs strongly-typed (nem.Contracts). `ConversationId`, `MessageId`, `AgentId`.
- **Channel Adapters**: Isolated, deployable separately. Common `IChannelAdapter` interface.
- **LLM Routing**: LiteLLM proxy. Model-ID normalization (OpenAI, Azure, Ollama, Anthropic).
- **Agent Hierarchy**: MCP tool integration for cross-service capabilities.
- **Memory**: Triple-store (subject-predicate-object) for context + knowledge.

## Solution + Docker

| File | Format | Purpose |
|------|--------|---------|
| `nem.Mimir.slnx` | .slnx (canonical format) | Solution file |
| `docker/api/Dockerfile` | — | API service container |
| `docker/sandbox/Dockerfile` | — | Sandbox environment |
| `docker/telegram/Dockerfile.telegram` | — | Telegram adapter |
| `src/mimir-chat/Dockerfile` | — | Next.js frontend |

## Conventions

- MediatR for CQRS (NEVER Wolverine in Mimir)
- Clean Architecture: Api → Application → Domain ← Infrastructure
- Strongly-typed IDs only — no raw Guid/string
- File-scoped namespaces, sealed classes, nullable, TreatWarningsAsErrors

## Tests

- xUnit v3 + NSubstitute
- Run: `dotnet test nem.Mimir.slnx`

## Quick Commands

```bash
dotnet build nem.Mimir.slnx
dotnet test nem.Mimir.slnx
dotnet run --project src/nem.Mimir.Api
cd src/mimir-chat && npm install && npm run dev
```
