# Deployment & Operations Guide: nem.Mimir

This guide covers the deployment, configuration, and operational aspects of nem.Mimir.

## Prerequisites

- **Container Runtime**: Docker 25+ or Podman 4.5+
- **Orchestration**: Docker Compose v2 (local/dev) or Kubernetes (production)
- **Database**: PostgreSQL 16+ with extensions (uuid-ossp)
- **Messaging**: RabbitMQ 3.12+ with Management plugin
- **Identity**: Keycloak 24+

## Environment Variables Reference

The following environment variables can be used to configure the system.

### Core Database
| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | - |
| `Database__ConnectionString` | Legacy alias for the connection string | - |

### Messaging (RabbitMQ)
| Variable | Description | Default |
|----------|-------------|---------|
| `RabbitMQ__ConnectionString` | Connection string (amqp://user:pass@host:port) | - |

### AI Orchestration (LiteLLM)
| Variable | Description | Default |
|----------|-------------|---------|
| `LiteLlm__BaseUrl` | URL of the LiteLLM proxy instance | `http://localhost:4000` |
| `LiteLlm__ApiKey` | Master API key for LiteLLM | - |
| `LiteLlm__TimeoutSeconds` | Timeout for model requests | `120` |
| `LiteLlm__DefaultModel` | Model used if none specified in request | `gpt-4o` |

### Authentication (OIDC)
| Variable | Description | Default |
|----------|-------------|---------|
| `Jwt__Authority` | The base URL of the Keycloak realm | - |
| `Jwt__Audience` | The Client ID registered in Keycloak | `mimir-api` |
| `Jwt__RequireHttpsMetadata` | Whether to require HTTPS for metadata discovery | `true` |

### Telegram Bot
| Variable | Description | Default |
|----------|-------------|---------|
| `Telegram__BotToken` | Bot token from @BotFather | - |
| `Telegram__ApiBaseUrl` | URL of the Mimir API | `http://localhost:5000` |
| `Telegram__KeycloakUrl` | Keycloak URL for bot-to-API auth | - |
| `Telegram__KeycloakClientId` | Client ID for service account | `mimir-api` |
| `Telegram__KeycloakClientSecret` | Client secret for service account | - |

## Local Deployment (Docker Compose)

1. Create a `.env` file based on `.env.example`.
2. Run `docker compose up -d`.
3. The system will auto-provision the database schema on first run via EF Core migrations and Marten auto-creation.

## Production Deployment (Kubernetes)

Mimir is designed to be cloud-native. Use the following patterns:

- **Deployment**: Use separate Deployments for `mimir-api` and `mimir-sync`.
- **Scaling**: `mimir-api` can be scaled horizontally. `mimir-sync` can also be scaled, as Wolverine handles competing consumers correctly.
- **Secrets**: Use Kubernetes Secrets or an external vault (Azure Key Vault, HashiCorp Vault) and map them to environment variables.
- **Probes**: 
  - Liveness: `/health`
  - Readiness: `/health`

## .NET Aspire Support

Mimir is compatible with .NET Aspire. You can include it in an Aspire orchestration by adding the projects to your `AppHost`.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
                      .WithDataBindMount("./data/postgres");

var db = postgres.AddDatabase("mimir-db");

var rabbitmq = builder.AddRabbitMQ("messaging");

var api = builder.AddProject<Projects.Mimir_Api>("mimir-api")
                 .WithReference(db)
                 .WithReference(rabbitmq);

builder.Build().Run();
```

## Monitoring & Logging

- **Logs**: Mimir uses Serilog. In production, configure it to write to a log aggregator (Elasticsearch, Seq, or stdout for Grafana Loki).
- **Health Checks**: The `/health` endpoint returns a JSON report of database and messaging health.
- **Correlation**: All logs include a `CorrelationId` to trace requests across the API and background workers.

## Troubleshooting

### Database Connectivity
If the API fails to start, verify the `ConnectionStrings__DefaultConnection` is valid and the PostgreSQL instance allows connections from the API's IP/network.

### Token Validation Failures
If requests return 401 Unauthorized:
1. Check that `Jwt__Authority` is reachable from the API container.
2. Verify that the `iss` (issuer) claim in the JWT exactly matches the `Jwt__Authority` value.
3. Ensure the token has not expired.

### LiteLLM Connection
If chat completions fail with 500:
1. Verify LiteLLM is running and `LiteLlm__BaseUrl` is correct.
2. Check LiteLLM logs for upstream provider errors (e.g., OpenAI rate limits).
