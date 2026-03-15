# nem.Mimir — Agent Notes ⚠️ LEGACY

> **⚠️ LEGACY** — Multi-channel conversational AI: Telegram, Web, Console, MCP. LLM routing (LiteLLM), agent hierarchy, MediatR CQRS + Wolverine sync. **The ACTIVE version is `nem.Mimir-typed-ids` — develop there, NOT here.**

## Status

| Repo | Status | Notes |
|------|--------|-------|
| **nem.Mimir** (this repo) | ⚠️ **LEGACY** | Pre-typed-IDs. Superseded. No active development. |
| **nem.Mimir-typed-ids** | ✓ **ACTIVE** | Refactored with strongly-typed IDs from nem.Contracts. Current branch. |

> Cross-reference: `nem.Mimir-typed-ids/AGENTS.md` for the ACTIVE version. Legacy central AI/chat: Telegram, Web, Console, MCP adapters, LiteLLM routing, agent orchestration, conversation memory.

## Projects

| Project | Layer | Role |
|---------|-------|------|
| `Mimir.Api` | Entry | HTTP/WebSocket API host |
| `Mimir.Application` | App | MediatR CQRS handlers, commands, queries |
| `Mimir.Domain` | Core | Entities, aggregates, domain events |
| `Mimir.Infrastructure` | Infra | DB, LiteLLM client, external adapters |
| `Mimir.Sync` | Sync | **Wolverine** messaging, cross-service sync |
| `Mimir.Telegram` | Adapter | Telegram bot adapter |
| `Mimir.Tui` | Client | Terminal UI client |
| `mimir-chat/` | Frontend | Next.js web client (non-.NET) |

## Key Patterns

- **Dual messaging stack** — UNIQUE in the ecosystem:
  - **MediatR**: Application-layer CQRS (commands/queries/handlers in `Mimir.Application`)
  - **Wolverine**: Messaging + sync (`Mimir.Sync/Configuration/WolverineConfiguration.cs`)
- **LLM Routing**: LiteLLM proxy (`litellm_config.yaml`). Supports OpenAI, Azure, Ollama, Anthropic.
- **Agent Hierarchy**: MCP tool integration for cross-service capabilities.
- **Auth**: Keycloak (`keycloak/`) — realm export, UNIFI SAML setup.
- **Docs**: `docs/` contains own ADRs, architecture, security, changelog.

## Solution + Docker

| File | Format | Purpose |
|------|--------|---------|
| `nem.Mimir.slnx` | `.slnx` (canonical format) | Solution entry point |
| `docker/api/Dockerfile` | — | API service container |
| `docker/sandbox/Dockerfile` | — | Sandbox environment |
| `docker/telegram/Dockerfile.telegram` | — | Telegram adapter |
| `src/mimir-chat/Dockerfile` | — | Next.js frontend |

## Tests

| Project | Type |
|---------|------|
| `Mimir.Api.Tests` | Unit |
| `Mimir.Api.IntegrationTests` | Integration |
| `Mimir.Application.Tests` | Unit |
| `Mimir.Domain.Tests` | Unit |
| `Mimir.Infrastructure.Tests` | Unit |
| `Mimir.Sync.Tests` | Unit |
| `Mimir.Telegram.Tests` | Unit |
| `Mimir.Tui.Tests` | Unit |
| `Mimir.Integration.Tests` | Integration |
| `Mimir.E2E.Tests` | E2E |

## Conventions

- MediatR CQRS for app layer; Wolverine for messaging/sync — both coexist
- Clean Architecture: `Api → Application → Domain ← Infrastructure`
- No typed IDs here (legacy) — use nem.Mimir-typed-ids for strongly-typed IDs
- `global.json` pins .NET SDK; `Directory.Build.props` + `Directory.Packages.props` for centralized builds
- `docs/adr/` for Architecture Decision Records

## Quick Commands

```bash
dotnet build nem.Mimir.slnx
dotnet test nem.Mimir.slnx
docker-compose up
cd src/mimir-chat && npm install && npm run dev
```
