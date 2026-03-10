# nem.Mimir-typed-ids — Agent Notes

> THE active Mimir. Conversational AI service — multi-channel chat, LLM routing, agent hierarchy, triple-store memory, MCP tool integration.

## Overview

Central AI/chat service for the nem.* ecosystem. Routes conversations across channels (Telegram, Teams, Console, Web), manages LLM provider abstraction via LiteLLM, implements agent hierarchy with tool orchestration, and maintains conversation memory in a triple store. This is the actively developed version (not `nem.Mimir` which is legacy).

## Structure

```
nem.Mimir-typed-ids/src/
├── nem.Mimir.Api/                   # ASP.NET host, endpoints, middleware, SignalR
├── nem.Mimir.Application/           # MediatR handlers, commands, queries, DTOs
├── nem.Mimir.Domain/                # Entities, value objects, strongly-typed IDs
├── nem.Mimir.Infrastructure/        # Persistence, LLM clients, external integrations
├── nem.Mimir.Adapter.Console/       # Console channel adapter
├── nem.Mimir.Adapter.MCP/           # MCP tool integration adapter
├── nem.Mimir.Adapter.Teams/         # Microsoft Teams channel adapter
├── nem.Mimir.Adapter.Telegram/      # Telegram Bot API adapter
├── nem.Mimir.Adapter.Web/           # Web/WebSocket channel adapter
├── nem.Mimir.Sync/                  # Synchronization service
├── nem.Mimir.Tui/                   # Terminal UI client
└── mimir-chat/                      # Next.js web chat frontend
```

## Key Patterns

- **MediatR CQRS**: `SendMessageCommand` → handler pipeline. NOT Wolverine (unlike MCP/KnowHub). Never mix.
- **Channel Adapters**: Each channel (Telegram, Teams, Web, Console, MCP) is an isolated adapter project. Common `IChannelAdapter` interface.
- **LLM Routing**: LiteLLM proxy for provider abstraction. Model-ID normalization map handles provider differences. Support for OpenAI, Azure, Ollama, Anthropic.
- **Agent Hierarchy**: Agents with tool definitions. MCP tool integration for cross-service capabilities.
- **Triple-Store Memory**: Conversation context and knowledge stored as triples (subject-predicate-object).
- **Typed IDs**: All entity IDs are strongly-typed (from nem.Contracts). `ConversationId`, `MessageId`, `AgentId`, etc.

## Where to Look

| Need | Location |
|------|----------|
| API endpoints | `src/nem.Mimir.Api/` |
| Command/query handlers | `src/nem.Mimir.Application/` |
| Domain entities | `src/nem.Mimir.Domain/` |
| LLM integration | `src/nem.Mimir.Infrastructure/` |
| Channel adapters | `src/nem.Mimir.Adapter.*/` |
| MCP tool bridge | `src/nem.Mimir.Adapter.MCP/` |
| Web chat UI | `src/mimir-chat/` (Next.js) |
| Model normalization | Look for model-ID mapping/normalization in Infrastructure |

## Conventions

- MediatR for CQRS (NOT Wolverine — that's MCP/KnowHub)
- Clean Architecture: Api → Application → Domain ← Infrastructure
- Channel adapters are independent — each can be deployed separately
- Strongly-typed IDs everywhere — no raw Guid/string identifiers
- File-scoped namespaces, sealed classes, nullable, TreatWarningsAsErrors

## Tests

- xUnit v3 + NSubstitute
- Run: `dotnet test nem.Mimir.sln`

## Commands

```bash
dotnet build nem.Mimir.sln
dotnet test nem.Mimir.sln
dotnet run --project src/nem.Mimir.Api
# Chat UI:
cd src/mimir-chat && npm install && npm run dev
```

## Anti-Patterns

- Never use Wolverine in Mimir — MediatR only
- Never use primitive IDs — always strongly-typed
- Never hardcode LLM provider URLs — use LiteLLM proxy
- Never add channel-specific logic to Application layer — keep in adapters
- `nem.Mimir` (without `-typed-ids`) is LEGACY — do not develop there
