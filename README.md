# nem.Mimir

> Enterprise AI Digital Brain built on .NET 10 — multi-LLM orchestration via LiteLLM, agent hierarchy with triple-store memory, OpenSandbox K8s-native code execution, real-time streaming via SignalR, and Keycloak-powered identity management.

## Architecture Overview

nem.Mimir is a distributed enterprise AI platform utilizing a modular monolith approach with asynchronous processing. The system orchestrates multiple Large Language Models (LLMs) through a LiteLLM proxy, providing real-time response streaming via SignalR and robust identity management through Keycloak.

### Core Components

- **Agent Hierarchy**: Hierarchical agent model (Reasoner, Explorer, Librarian, Specialist) with parent-child delegation, powered by `nem.Cognitive.*` packages.
- **Triple-Store Memory**: Working (conversation buffer), Episodic (session summaries), and Semantic (knowledge graph via Apache AGE) memory tiers.
- **MCP Tools Integration**: Model Context Protocol tool support for external system interaction via standardized MCP servers, with dynamic discovery and tool chaining.
- **OpenSandbox Execution**: K8s-native managed sandbox replacing Docker.DotNet for secure, isolated code execution with automatic scaling.
- **Token Optimization**: Adaptive token budgeting, sliding-window context management, semantic caching, and model cascading for 40-60% cost reduction.

Detailed architectural decisions and patterns are documented in [ADR-004-architecture-overview.md](docs/adr/ADR-004-architecture-overview.md).

## Dependencies

| Dependency | Purpose |
| :--- | :--- |
| **nem.KnowHub** | Knowledge management, RAG, cognitive agent packages (`nem.Cognitive.*`) |
| **nem.MCP** | Master Control Program — central configuration, deployment, monitoring |
| **nem.AppHost** | .NET Aspire orchestration, service discovery, resource provisioning |
| **LiteLLM** | Multi-LLM proxy (OpenAI, Ollama, Azure OpenAI) |
| **Keycloak** | OIDC authentication and authorization |
| **PostgreSQL** | Primary data store + pgvector + Marten event store |
| **RabbitMQ** | Async messaging via Wolverine |

## Quick Start

### Prerequisites
- .NET 10 SDK
- Docker & Docker Compose v2
- PostgreSQL 16+ (or use Docker)
- Keycloak 24+ (or use Docker)
- RabbitMQ 3.x (or use Docker)
- Node.js 20+ (for mimir-chat frontend)

### Using Docker Compose (recommended)
```bash
cp .env.example .env
docker compose up -d
```
- API: `http://localhost:5000`
- mimir-chat: `http://localhost:3000`
- Keycloak: `http://localhost:8080`
- RabbitMQ Management: `http://localhost:15672`

### Local Development
```bash
dotnet restore
dotnet run --project src/Mimir.Api       # REST API + SignalR hub
dotnet run --project src/Mimir.Tui       # Terminal UI client
dotnet run --project src/Mimir.Telegram  # Telegram bot
cd src/mimir-chat && npm install && npm run dev  # Next.js frontend
```

## Project Structure

```
src/
  Mimir.Api/            ASP.NET Core Web API + SignalR hub
  Mimir.Application/    CQRS handlers, MediatR, FluentValidation, Mapperly
  Mimir.Domain/         Entities, value objects, enums, typed IDs
  Mimir.Infrastructure/ EF Core, repositories, services, sandbox
  Mimir.Sync/           Wolverine message handlers
  Mimir.Tui/            Terminal UI client
  Mimir.Telegram/       Telegram bot
  mimir-chat/           Next.js frontend
tests/                  9 test projects (1,022+ tests)
docs/                   ADRs, security, data model, deployment
```

## Configuration

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Jwt__Authority` | OIDC provider URL (Keycloak) |
| `Jwt__Audience` | Registered audience for JWT validation |
| `RabbitMQ__Host` / `Port` / `User` / `Password` | RabbitMQ connection |
| `LiteLlm__BaseUrl` | Base URL for LiteLLM proxy |
| `Telegram__BotToken` | Token for the Telegram bot |
| `Cors__AllowedOrigins` | Permitted CORS origins |

## Testing

```bash
dotnet test                                        # All 1,022+ tests
dotnet test --collect:"XPlat Code Coverage"        # With coverage
```

**Frameworks**: xUnit v3, Shouldly, NSubstitute, Testcontainers (PostgreSQL, RabbitMQ), WireMock.Net

## Security

- **Authentication**: OIDC via Keycloak with JWT Bearer validation
- **Authorization**: Role-based access control (Admin, User)
- **Network**: HSTS, CSP headers, CORS policy, rate limiting
- **Data**: Input sanitization and output encoding middleware
- **Isolation**: Code execution via OpenSandbox K8s pods (gVisor/Kata isolation)

See [docs/security-architecture.md](docs/security-architecture.md) for details.

## Documentation

See [Architecture Documentation](../docs/architecture/index.md)

- [Architecture Overview](docs/adr/ADR-004-architecture-overview.md)
- [Data Model](docs/data-model.md)
- [Security Architecture](docs/security-architecture.md)
- [Deployment Runbook](docs/deployment-runbook.md)
- [Security Threat Model](docs/security-threat-model.md)
- [ADR-001: OpenChat UI Pivot](docs/adr/ADR-001-openchat-ui-pivot.md)
- [ADR-002: Plugin Architecture](docs/adr/ADR-002-plugin-architecture.md)
- [ADR-003: Wolverine Messaging](docs/adr/ADR-003-wolverine-messaging.md)

## License

See LICENSE file for details.
