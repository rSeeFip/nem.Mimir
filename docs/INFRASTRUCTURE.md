---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Infrastructure Documentation: nem.Mimir

## Runtime topology
Primary runtime is containerized via `docker-compose.yml` with these services:
- `mimir-api`
- `mimir-chat`
- `mimir-db` (PostgreSQL)
- `mimir-rabbitmq`
- `mimir-keycloak`
- `mimir-litellm`
- `mimir-telegram`
- `mimir-sandbox` (build profile for code execution image)

## Core infrastructure dependencies
- PostgreSQL 16 for application persistence.
- RabbitMQ management image for Wolverine transport.
- Keycloak for OIDC/JWT identity.
- LiteLLM proxy with mounted `litellm_config.yaml`.
- Docker daemon access for sandbox execution service.

## API host infrastructure wiring
`Program.cs` and `DependencyInjection.cs` wire:
- Npgsql DbContext and interceptors
- LiteLLM HttpClient with retry + circuit breaker resilience
- Wolverine messaging with RabbitMQ durable patterns
- SignalR chat hub
- health checks for DB and LiteLLM URL probe

## Deployment artifact layout
- `docker/api/Dockerfile` for API image.
- `docker/telegram/Dockerfile.telegram` for bot worker.
- `src/mimir-chat/Dockerfile` for Next.js frontend.

## Configuration sources
Primary config files:
- `appsettings.json`
- `appsettings.Development.json`
- `appsettings.Production.json`
- environment variables (compose and host)

Critical sections:
- `LiteLlm:*`
- `ConnectionStrings:DefaultConnection`
- `RabbitMQ:ConnectionString`
- JWT/Keycloak values
- CORS allowed origins

## Capacity and resource constraints
Compose-level limits include:
- `mimir-api`: memory + PID limits
- `mimir-telegram`: memory/CPU/PID limits and read-only filesystem

Sandbox design assumptions:
- one-shot execution containers
- no exposed ports
- no network mode in execution containers (enforced by sandbox service runtime behavior)

## Health and observability
- `/health` endpoint with DB and LiteLLM probes.
- Serilog request and application logging.
- correlation ID middleware for trace linking.
- background health reporter publishes `ServiceHealthReport` via Wolverine.

## Operational run patterns
### Local startup
`docker compose up -d` with configured environment values.

### Build/test
- `dotnet build nem.Mimir.slnx`
- `dotnet test nem.Mimir.slnx`

### MCP runtime behavior
- enabled MCP server configs are auto-connected on startup.
- config change listener polls and refreshes connections.

## Infrastructure risks and caveats
- legacy/active branch split can create drift in deployment assumptions.
- model naming in code and LiteLLM config may diverge.
- Dockerfile references use both `.sln` and `.slnx` conventions in repo history; keep release scripts aligned.

## Recovery and rollback notes
- DB restore strategy is external to app; app supports soft-delete restore for selected entities.
- Service rollback is image-tag based in compose/Kubernetes style deployments.
- MCP misconfiguration can be isolated by disabling specific server configs.

## Cross-references
- [ARCHITECTURE](./ARCHITECTURE.md)
- [SECURITY](./SECURITY.md)
- [COMPLIANCE](./COMPLIANCE.md)
- [USER-MANUAL](./USER-MANUAL.md)
