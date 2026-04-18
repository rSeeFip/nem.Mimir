# nem.Mimir-typed-ids/src — Agent Notes

> 12 projects: Clean Architecture core + 6 channel adapters + chat UI. Wolverine CQRS (migrated from MediatR per .sisyphus/plans/nem-gold-standard.md#T12).

## Projects

| Project | Purpose |
|---------|---------|
| `Mimir.Api` | ASP.NET host — endpoints, SignalR hubs, middleware |
| `Mimir.Application` | MediatR handlers — commands, queries, DTOs, pipelines |
| `Mimir.Domain` | Entities, value objects, strongly-typed IDs |
| `Mimir.Infrastructure` | Persistence, LiteLLM client, provider integrations |
| `Mimir.Telegram` | Telegram Bot API channel adapter |
| `Mimir.Teams` | Microsoft Teams channel adapter |
| `Mimir.WhatsApp` | WhatsApp channel adapter |
| `Mimir.Signal` | Signal messaging adapter |
| `Mimir.Voice` | Voice/audio channel adapter |
| `Mimir.Sync` | Cross-channel synchronization service |
| `Mimir.Tui` | Terminal UI client (Spectre.Console or similar) |
| `mimir-chat` | Next.js web chat frontend |

## Dependency Flow

```
Api → Application, Infrastructure, Adapters
Application → Domain (implements use cases via MediatR)
Domain → (no project dependencies — pure domain)
Infrastructure → Domain (implements persistence/external interfaces)
Adapters → Application (send MediatR commands)
```

## Key Files

| Need | Where |
|------|-------|
| MediatR commands | `Mimir.Application/Commands/` |
| MediatR queries | `Mimir.Application/Queries/` |
| Domain entities | `Mimir.Domain/Entities/` |
| LLM integration | `Mimir.Infrastructure/Llm/` or `Mimir.Infrastructure/Providers/` |
| Channel adapter interface | Look for `IChannelAdapter` in Application or Domain |
| Web chat frontend | `mimir-chat/` (Next.js App Router) |

## Conventions

- Wolverine CQRS (migrated from MediatR per .sisyphus/plans/nem-gold-standard.md#T12)
- Each adapter is independent and deployable separately
- Channel-specific logic stays in adapter project — not in Application
- Strongly-typed IDs for all entities
- `SendMessageCommand` is the primary entry point for all channels
