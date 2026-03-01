# ADR-004: Architecture Overview

## Status
Accepted

## Date
2026-03-01

## Context
nem.Mimir is an enterprise AI chat platform requiring multi-LLM support, real-time streaming, plugin extensibility, and role-based access control. This ADR documents the architectural decisions that shape the system.

## Decision

### System Architecture
```mermaid
graph TD
    subgraph Client Layer
        TUI[Mimir.Tui]
        WEB[mimir-chat Next.js]
        BOT[Mimir.Telegram]
    end

    subgraph API Layer
        API[Mimir.Api ASP.NET Core]
        HUB[ChatHub SignalR]
    end

    subgraph Application Layer
        APP[Mimir.Application]
        MED[MediatR CQRS]
        VAL[FluentValidation]
        MAP[Mapperly]
    end

    subgraph Domain Layer
        DOM[Mimir.Domain Entities/Events]
    end

    subgraph Infrastructure Layer
        INF[Mimir.Infrastructure]
        EF[EF Core PostgreSQL]
        LLM[LiteLLM Service]
        DOCKER[Docker Sandbox]
        PLUGIN[Plugin Manager]
    end

    subgraph Messaging Layer
        SYNC[Mimir.Sync Wolverine]
    end

    subgraph External
        KC[Keycloak OIDC]
        DB[(PostgreSQL)]
        RMQ[RabbitMQ]
        AI[LiteLLM API]
    end

    TUI & WEB & BOT --> API & HUB
    API & HUB --> MED
    MED --> APP
    APP --> DOM
    APP --> INF
    INF --> EF & LLM & DOCKER & PLUGIN
    EF --> DB
    LLM --> AI
    API --> RMQ
    RMQ --> SYNC
    API & SYNC --> KC
```

### Request Flows

#### HTTP Request Flow
```mermaid
sequenceDiagram
    participant C as Client
    participant A as API Controller
    participant M as MediatR
    participant H as Command/Query Handler
    participant R as Repository
    participant D as Database

    C->>A: HTTP Request (JWT)
    A->>M: Send Command/Query
    M->>H: Dispatch to Handler
    H->>R: Request Data
    R->>D: SQL Query
    D-->>R: Data
    R-->>H: Entity/Collection
    H-->>M: Result/DTO
    M-->>A: Result
    A-->>C: HTTP Response (ProblemDetails if error)
```

#### SignalR Streaming Flow
```mermaid
sequenceDiagram
    participant C as Client
    participant H as ChatHub
    participant S as ContextWindowService
    participant L as LiteLLM (SSE Stream)
    participant R as Repository

    C->>H: SendMessage(conversationId, content)
    H->>S: BuildLlmMessagesAsync
    S-->>H: Message List
    H->>R: Persist User Message
    H->>L: StreamMessageAsync
    loop Tokens
        L-->>H: Token Chunk
        H-->>C: ChatToken (SignalR)
    end
    L-->>H: Stream Complete
    H->>R: Persist Assistant Message
```

#### Async Messaging Flow (Wolverine)
```mermaid
sequenceDiagram
    participant A as API/Application
    participant R as RabbitMQ
    participant W as Wolverine Handler
    participant S as Side Effects

    A->>R: Publish Event (e.g., MessageSent)
    R->>W: Consume Message
    W->>S: Process (Audit, Sync, etc.)
```

### Layer Responsibilities
- **Domain**: Pure business logic with no infrastructure dependencies. Contains entities, value objects, enums, and domain events.
- **Application**: MediatR CQRS handlers, DTOs, FluentValidation rules, Mapperly source-generated mappings, and interfaces for infrastructure services.
- **Infrastructure**: Implementation of repositories, EF Core DbContext, external service integrations (LiteLLM, Docker), and persistence interceptors for audit and soft delete.
- **API**: Controllers, SignalR hubs, middleware pipeline (logging, error handling, sanitization), and dependency injection composition root.
- **Sync**: Wolverine message handlers for asynchronous background processing and event-driven side effects.
- **Tui/Telegram**: Standalone client applications that consume the API, each maintaining its own DI and configuration.

### Cross-Cutting Concerns
- **Authentication**: Keycloak OIDC handles identity. API validates JWT Bearer tokens and maps Keycloak roles to standard policy-based authorization.
- **Audit Trail**: EF Core SaveChanges interceptor automatically tracks creation and modification metadata (CreatedAt, CreatedBy, UpdatedAt, UpdatedBy).
- **Soft Delete**: Global query filters combined with an interceptor convert hard deletes into soft deletes by setting IsDeleted=true.
- **Correlation IDs**: Middleware ensures correlation IDs propagate through the entire request pipeline for structured logging.
- **Error Handling**: Global exception handler middleware converts internal errors into standard ProblemDetails responses.
- **Input Validation**: FluentValidation pipeline behaviors validate requests before they reach handlers.
- **Rate Limiting**: Per-user fixed window rate limiting applied to both HTTP controllers and SignalR hubs.

### Federation Independence
Mimir operates independently from the broader nem.* ecosystem. While other nem.* services may share federation patterns, Mimir maintains its own:
- Identity management via private Keycloak instance
- Primary data storage in PostgreSQL
- Messaging backbone via RabbitMQ/Wolverine
- No shared service mesh or API gateway dependencies

## Consequences
- Clean separation enables independent testing of each layer.
- CQRS via MediatR provides clear command/query separation.
- Source-generated mapping (Mapperly) eliminates runtime reflection overhead.
- Wolverine provides reliable async messaging with built-in retry logic.
- Plugin sandbox (Docker) provides security isolation for custom code execution.
