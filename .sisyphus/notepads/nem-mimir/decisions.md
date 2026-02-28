# nem.Mimir Architectural Decisions

## ADR-001: Replace Angular Chat UI with OpenOrca/openchat-ui via nem.Messaging (Wolverine)

**Date**: 2026-02-28
**Status**: APPROVED by user
**Architecture Path**: C — OpenAI-compatible SSE endpoints on Mimir.Api + openchat-ui as thin frontend

### Context
- User requested removal of custom Angular 19 Chat UI (mimir-web)
- Replace with OpenOrca/openchat-ui (fork of chatbot-ui) as standard UI for ALL nem.* apps
- Integration must flow through nem.Messaging using Wolverine message bus
- Must align with nem.* ecosystem patterns (observed from nem.HomeAssistant)

### Decision

**1. Frontend: openchat-ui (Next.js 13 + React 18 + Tailwind CSS)**
- Fork OpenOrca/openchat-ui into `src/mimir-chat/`
- Add Keycloak OIDC auth (next-auth + keycloak provider)
- Point OPENAI_API_HOST at Mimir.Api (not directly at LiteLLM)
- Preserve: markdown rendering, code highlighting, streaming, conversation management
- Add: nem.* branding/theming, persistent storage via Mimir.Api REST

**2. Backend: OpenAI-compatible SSE endpoints on Mimir.Api**
- Add `/v1/chat/completions` (POST, SSE streaming) → proxies to LiteLLM
- Add `/v1/models` (GET) → returns available models from LiteLLM
- These endpoints go through our auth + audit pipeline
- Every chat request: JWT auth → audit log → Wolverine event → LiteLLM → SSE response

**3. Wolverine Messaging Layer (Mimir.Sync project)**
- New project: `src/Mimir.Sync/` following nem.HomeAssistant pattern
- Wolverine handlers for: ChatCompleted, ConversationCreated, MessageSent, AuditEvent
- Publishers emit events to nem.Messaging bus (RabbitMQ transport)
- Other nem.* services can subscribe to Mimir events
- Cascading pattern: HTTP endpoint → (SSE response, WolverineMessage)

**4. Admin UI**
- Admin dashboard remains as a route in openchat-ui (custom Next.js pages)
- OR: Defer to nem.MCP Dashboard for admin/monitoring (per nem.* pattern)
- Decision: Minimal admin in openchat-ui, full admin in MCP Dashboard

**5. Standard for all nem.* apps**
- openchat-ui becomes `nem.Chat` — a reusable Docker image
- Configure via env vars: API host, Keycloak realm, branding
- Each nem.* app can include nem.Chat as a Docker Compose service

### Architecture Flow
```
Browser → openchat-ui (Next.js :3000)
            ↓ POST /v1/chat/completions
       Mimir.Api (ASP.NET :5001)
            ↓ JWT auth + audit
       Mimir.Sync (Wolverine)
            ↓ publish ChatRequestReceived
       nem.Messaging (RabbitMQ)
            ↓ (async: audit, analytics, notifications)
       Mimir.Api → LiteLLM (:4000) → LM Studio (:1234)
            ↓ SSE stream back
       Browser (real-time token display)
```

### nem.* Pattern Alignment (from nem.HomeAssistant)
- Core: Domain models, interfaces → already have (Mimir.Domain + Mimir.Application)
- Sync: Wolverine handlers/publishers → NEW (Mimir.Sync)
- Persistence: EF Core → already have (Mimir.Infrastructure)
- Plugin: DI wiring, hosted services → already have (Mimir.Api)

### Consequences
- REMOVE: src/mimir-web/ (Angular 19 SPA, ~34 files)
- REMOVE: Angular-specific npm packages, ng build scripts
- ADD: src/mimir-chat/ (openchat-ui fork, Next.js)
- ADD: src/Mimir.Sync/ (Wolverine messaging)
- MODIFY: Mimir.Api (add /v1/* SSE endpoints, add Wolverine integration)
- MODIFY: docker-compose.yml (replace angular with next.js, add RabbitMQ)
- KEEP: All backend (.NET) — Domain, Application, Infrastructure, Api remain
- KEEP: TUI client, Telegram bot — unchanged
- KEEP: Keycloak, PostgreSQL, LiteLLM — unchanged
- ADJUST: T27-T30 (Wave 5) are now obsolete for Angular, replaced by new tasks
- ADJUST: T28 streaming → built into openchat-ui natively (SSE)
- ADJUST: T29 admin → minimal in openchat-ui, defer full admin to MCP Dashboard
