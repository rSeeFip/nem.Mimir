# Nem.Mimir

AI-powered chat platform with multi-channel support (Web, TUI, Telegram).

## Architecture

```
┌─────────────────┐     ┌──────────────┐     ┌─────────────────┐
│  mimir-chat     │────▶│   Mimir.Api  │────▶│   Mimir.Sync    │
│  (Next.js UI)   │     │   (ASP.NET)  │     │  (Wolverine)    │
└─────────────────┘     └──────────────┘     └─────────────────┘
                              │                        │
                              ▼                        ▼
                       ┌──────────────┐         ┌──────────────┐
                       │   Keycloak  │         │    RabbitMQ  │
                       │   (Auth)    │         │   (Messages) │
                       └──────────────┘         └──────────────┘
                              │
                              ▼
                       ┌──────────────┐
                       │  PostgreSQL  │
                       │   (Data)     │
                       └──────────────┘
```

## Services

| Service | Description | Port |
|---------|-------------|------|
| `Mimir.Api` | REST API + SignalR hub | 5000 |
| `Mimir.Sync` | Wolverine message processor | - |
| `Mimir.Tui` | Terminal UI client | - |
| `Mimir.Telegram` | Telegram bot | - |
| `mimir-chat` | Next.js web UI | 3000 |
| `Keycloak` | Authentication | 8080 |
| `RabbitMQ` | Message broker | 5672 |
| `sandbox` | Code execution plugin | - |

## Quick Start

### Prerequisites
- .NET 8 SDK
- Node.js 20+
- Docker & Docker Compose
- PostgreSQL (optional, via Docker)

### Development

```bash
# Clone and setup
git clone https://github.com/yourorg/nem.Mimir.git
cd nem.Mimir

# Start infrastructure
docker-compose up -d db keycloak rabbitmq

# Run API
cd src/Mimir.Api
dotnet run

# Run chat UI (separate terminal)
cd src/mimir-chat
npm install
npm run dev
```

### Production (Docker)

```bash
docker-compose up -d
```

## Configuration

Environment variables:
- `DATABASE_URL` - PostgreSQL connection
- `KEYCLOAK_URL` - Keycloak server
- `RABBITMQ_URL` - RabbitMQ connection
- `LITELLM_URL` - LLM proxy endpoint

## Plugins

Built-in plugins:
- **CodeRunner** - Execute code in isolated sandbox
- **WebSearch** - Web search capability (stub)

## Security

- Authentication via Keycloak (OAuth2/OIDC)
- CSP + HSTS headers in production
- Correlation IDs for request tracing
- Plugin isolation via AssemblyLoadContext

## Testing

```bash
# Unit tests
dotnet test --filter "FullyQualifiedName~Mimir.Application.Tests"

# E2E tests (requires Docker)
dotnet test --filter "FullyQualifiedName~Mimir.E2E.Tests"
```

## License

MIT
