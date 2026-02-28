# nem.Mimir — UI Pivot Execution Plan
# Replace Angular Chat UI with openchat-ui + Wolverine Messaging
# Created: 2026-02-28
# Architecture: Path C (ADR-001)

## STATUS: Waves 1-4 COMPLETE (T1-T25), T26 COMPLETE, T27-T30 OBSOLETE (Angular), New tasks below

---

## Wave 5-PIVOT: Remove Angular, Add Wolverine Sync Layer
> Sequential — each builds on previous

- [ ] P1: Remove Angular mimir-web project (delete src/mimir-web/, remove from solution, clean angular.json refs, remove build-web.sh, remove wwwroot from .gitignore)
- [ ] P2: Add Mimir.Sync project — Wolverine messaging layer (src/Mimir.Sync/Mimir.Sync.csproj, Wolverine+RabbitMQ packages, message types: ChatRequestReceived, ChatCompleted, MessageSent, ConversationCreated, AuditEventPublished; handlers: ChatCompletedHandler → audit log, MessageSentHandler → persist + publish)
- [ ] P3: Integrate Wolverine into Mimir.Api (AddWolverine + UseWolverine in Program.cs, RabbitMQ transport config, reference Mimir.Sync, coexist with existing MediatR handlers)
- [ ] P4: Add OpenAI-compatible SSE endpoints to Mimir.Api — POST /v1/chat/completions (JWT auth → validate → proxy to LiteLLM with SSE streaming → publish ChatCompleted via Wolverine), GET /v1/models (proxy to LiteLLM /v1/models, filter+return)
- [ ] P5: Add RabbitMQ to docker-compose.yml (rabbitmq:3-management, port 5672/15672, health check, mimir-api depends_on rabbitmq)
- [ ] P6: Mimir.Sync unit tests (test all handlers, test message publishing, test SSE endpoint integration)

## Wave 6-PIVOT: openchat-ui Integration
> P7-P8 parallel, P9-P10 sequential after

- [ ] P7: Clone openchat-ui into src/mimir-chat/ (git subtree or copy, strip .git, update package.json name to @nem/mimir-chat, verify npm install + npm run build works standalone)
- [ ] P8: Add Keycloak OIDC auth to openchat-ui (next-auth + @next-auth/keycloak-provider OR custom middleware, protect all routes, pass JWT to API calls, remove API key input UI, add user profile display)
- [ ] P9: Configure openchat-ui to use Mimir.Api (OPENAI_API_HOST → http://mimir-api:5001, update pages/api/chat.ts to forward JWT, update pages/api/models.ts to forward JWT, add conversation persistence via Mimir.Api REST endpoints instead of localStorage)
- [ ] P10: nem.* branding + theming (Tailwind config: nem.* color palette, logo, favicon, app name "nem.Mimir", dark mode default, responsive layout verification)

## Wave 7-PIVOT: Docker + Integration Testing
> P11-P12 parallel, P13 after both

- [ ] P11: openchat-ui Dockerfile + Docker Compose service (multi-stage Node 20-alpine, env vars for Keycloak + API host, add mimir-chat service to docker-compose.yml, depends_on mimir-api + keycloak, port 3000, health check)
- [ ] P12: Update Mimir.Api Program.cs — remove SPA hosting (UseStaticFiles, UseDefaultFiles, MapFallbackToFile, ResponseCompression for static), keep API-only mode, update CORS for openchat-ui origin (localhost:3000)
- [ ] P13: End-to-end integration test — full Docker Compose up (db + keycloak + litellm + rabbitmq + mimir-api + mimir-chat + sandbox), verify: Keycloak login → chat UI loads → send message → SSE streaming works → conversation persisted → Wolverine event published to RabbitMQ → audit logged

## Wave 8-ADJUSTED: Plugin System + Full Docker (from original plan, adjusted)
> These tasks carry forward from original Wave 6

- [ ] P14: Plugin Architecture (IPlugin interface, AssemblyLoadContext isolation, PluginManager, nem.Plugins.SDK adapter — unchanged from T31)
- [ ] P15: System Prompt + Conversation Configuration (ISystemPromptService, template variables, auto-archive — unchanged from T32)
- [ ] P16: Built-in Plugins — CodeRunner via sandbox + WebSearch stub (unchanged from T33)
- [ ] P17: Full Docker Compose — all services (8 total: db, keycloak, litellm, rabbitmq, sandbox, mimir-api, mimir-chat, telegram — adjusted from T34)

## Wave 9-ADJUSTED: Testing + Security + Performance (from original Wave 7)

- [ ] P18: API Integration Tests with Testcontainers (full auth flow, SSE streaming, Wolverine messaging — expanded from T35)
- [ ] P19: Security Hardening (HSTS, CSP, CORS, rate limiting, dependency audit for BOTH .NET and Node.js — expanded from T36)
- [ ] P20: Performance Optimization for M3 Pro Docker (resource limits, ReadyToRun, ServerGC, Next.js standalone build — adjusted from T37)
- [ ] P21: Serilog Structured Logging + Wolverine correlation IDs (expanded from T38)

## Wave 10-ADJUSTED: Documentation + QA (from original Waves 8 + FINAL)

- [ ] P22: E2E QA — all channels: openchat-ui, TUI, Telegram (adjusted from T39)
- [ ] P23: Architecture Decision Records (expanded with ADR-001 openchat-ui pivot — from T40)
- [ ] P24: README + Getting Started Guide (updated for new architecture — from T41)
- [ ] P25: Final compliance audit (F1)
- [ ] P26: Code quality review (F2)
- [ ] P27: Manual QA (F3)
- [ ] P28: Scope fidelity check against original requirements (F4)

---

## TASK DETAILS

### P1: Remove Angular mimir-web
**Category**: quick
**Files to DELETE**: entire `src/mimir-web/` directory
**Files to MODIFY**: 
- `nem.Mimir.sln` — remove mimir-web project reference (if any)
- `.gitignore` — remove `src/Mimir.Api/wwwroot/` entry
- `scripts/build-web.sh` — DELETE
- `src/Mimir.Api/Program.cs` — remove UseDefaultFiles, UseStaticFiles, MapFallbackToFile, ResponseCompression for static assets
**Verification**: `dotnet build` succeeds, no angular references remain

### P2: Add Mimir.Sync (Wolverine Messaging)
**Category**: deep
**New project**: `src/Mimir.Sync/Mimir.Sync.csproj`
**NuGet**: WolverineFx, WolverineFx.RabbitMQ
**Structure**:
```
src/Mimir.Sync/
├── Mimir.Sync.csproj
├── Messages/
│   ├── ChatRequestReceived.cs    # record(Guid ConversationId, Guid UserId, string Model, string Content)
│   ├── ChatCompleted.cs          # record(Guid ConversationId, Guid MessageId, string Model, int TokensUsed, TimeSpan Duration)
│   ├── MessageSent.cs            # record(Guid ConversationId, Guid MessageId, string Role, string Content)
│   ├── ConversationCreated.cs    # record(Guid ConversationId, Guid UserId, string Title)
│   └── AuditEventPublished.cs    # record(string Action, Guid UserId, string Details, DateTimeOffset Timestamp)
├── Handlers/
│   ├── ChatCompletedHandler.cs   # Logs to audit, updates conversation stats
│   ├── MessageSentHandler.cs     # Persists message, publishes to bus
│   └── AuditEventHandler.cs      # Writes to audit log table
├── Publishers/
│   └── MimirEventPublisher.cs    # IEventPublisher abstraction + Wolverine impl
└── Configuration/
    └── WolverineConfiguration.cs # Extension method for DI: AddMimirMessaging(opts)
```
**References**: Mimir.Application (for IRepository, IUnitOfWork, IAuditService)
**Pattern**: Follow nem.HomeAssistant.Sync structure

### P3: Wire Wolverine into Mimir.Api
**Category**: unspecified-low
**Modify**: `src/Mimir.Api/Program.cs`
**Add**:
```csharp
builder.Host.UseWolverine(opts => {
    opts.UseRabbitMq(new Uri(builder.Configuration.GetConnectionString("RabbitMQ")!))
        .AutoProvision()
        .EnableWolverineControlQueues();
    opts.Policies.UseDurableInboxOnAllListeners();
    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
    opts.Discovery.IncludeAssembly(typeof(Mimir.Sync.Messages.ChatCompleted).Assembly);
});
```
**Coexistence**: MediatR stays for existing command/query handlers. Wolverine handles cross-service events.
**NuGet**: Add WolverineFx + WolverineFx.RabbitMQ to Mimir.Api

### P4: OpenAI-compatible SSE Endpoints
**Category**: deep
**New files**:
- `src/Mimir.Api/Endpoints/V1/ChatCompletionsEndpoint.cs` — Wolverine.Http endpoint
- `src/Mimir.Api/Endpoints/V1/ModelsEndpoint.cs` — Wolverine.Http endpoint
- `src/Mimir.Api/Models/OpenAi/` — DTOs matching OpenAI API schema
**Behavior**:
- POST /v1/chat/completions: Auth → parse request → forward to LiteLLM (SSE) → stream back → publish ChatCompleted
- GET /v1/models: Auth → forward to LiteLLM → filter → return
**SSE format**: `data: {"id":"chatcmpl-xxx","object":"chat.completion.chunk","choices":[{"delta":{"content":"token"}}]}\n\n`

### P5: RabbitMQ in Docker Compose
**Category**: quick
**Modify**: `docker-compose.yml`
**Add**:
```yaml
mimir-rabbitmq:
  image: rabbitmq:3-management-alpine
  ports:
    - "5672:5672"
    - "15672:15672"
  environment:
    RABBITMQ_DEFAULT_USER: mimir
    RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD:-mimir-dev}
  volumes:
    - rabbitmq_data:/var/lib/rabbitmq
  healthcheck:
    test: ["CMD", "rabbitmq-diagnostics", "check_port_connectivity"]
    interval: 10s
    timeout: 5s
    retries: 5
```

### P7: Clone openchat-ui
**Category**: unspecified-high
**Source**: https://github.com/OpenOrca/openchat-ui
**Target**: `src/mimir-chat/`
**Steps**: Clone, strip .git, rename package to @nem/mimir-chat, npm install, npm run build verify

### P8: Add Keycloak Auth to openchat-ui
**Category**: deep
**Approach**: next-auth with keycloak provider
**New files**: pages/api/auth/[...nextauth].ts, middleware.ts (protect routes), components/Auth/
**Remove**: API key input dialog, localStorage API key storage
**Add**: Login redirect, JWT forwarding, user avatar/name display

### P9: Configure openchat-ui for Mimir.Api
**Category**: unspecified-high
**Modify**: 
- pages/api/chat.ts → forward JWT from session, use Mimir.Api /v1/chat/completions
- pages/api/models.ts → forward JWT, use Mimir.Api /v1/models
- utils/app/conversation.ts → replace localStorage with REST API calls to Mimir.Api /api/conversations
- .env.local → OPENAI_API_HOST=http://localhost:5001, NEXTAUTH_URL, KEYCLOAK_* vars

### P10: nem.* Branding
**Category**: visual-engineering
**Modify**: tailwind.config.js (nem.* colors), public/ (logo, favicon), components/ (app name, footer)

### P11: openchat-ui Docker
**Category**: quick
**New**: `docker/chat/Dockerfile` (Node 20-alpine multi-stage)
**Modify**: docker-compose.yml (add mimir-chat service)

### P12: Remove SPA hosting from Mimir.Api
**Category**: quick
**Modify**: Program.cs — strip UseDefaultFiles, UseStaticFiles, MapFallbackToFile, static file cache options
**Keep**: CORS updated for http://localhost:3000

---

## PARALLELIZATION MAP

```
Wave 5-PIVOT: P1 → P2 → P3 → [P4, P5] parallel → P6
Wave 6-PIVOT: [P7, P8] parallel → P9 → P10
Wave 7-PIVOT: [P11, P12] parallel → P13
Wave 8: [P14, P15] parallel → P16 → P17
Wave 9: [P18, P19] parallel → [P20, P21] parallel
Wave 10: P22 → P23 → P24 → [P25, P26, P27, P28] parallel
```
