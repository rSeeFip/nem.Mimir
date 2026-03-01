# Deployment Runbook — nem.Mimir

## Prerequisites
- Docker Engine 24+ / Docker Compose v2
- .NET 10 SDK (for non-Docker deployments)
- PostgreSQL 16+
- Keycloak 24+
- RabbitMQ 3.x with management plugin
- LiteLLM (for LLM proxy)

## Environment Variables Catalog
| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | Yes | - | PostgreSQL connection string |
| `Jwt__Authority` | Yes | - | Keycloak realm URL |
| `Jwt__Audience` | Yes | `mimir-api` | JWT audience identifier |
| `Jwt__RequireHttpsMetadata` | No | `true` | Require HTTPS for OIDC discovery |
| `RabbitMQ__Host` | Yes | `localhost` | RabbitMQ server address |
| `RabbitMQ__Port` | No | `5672` | RabbitMQ server port |
| `RabbitMQ__User` | No | `guest` | RabbitMQ username |
| `RabbitMQ__Password` | No | `guest` | RabbitMQ password |
| `LiteLLM__BaseUrl` | Yes | - | URL for the LiteLLM proxy service |
| `LiteLLM__ApiKey` | No | - | API key for LiteLLM if configured |
| `Telegram__BotToken` | No | - | Bot token from BotFather |
| `Cors__AllowedOrigins` | No | `[]` | Array of permitted origins |
| `Sanitization__MaxMessageLength` | No | `10000` | Maximum allowed characters per message |

## Docker Compose Deployment

### Development
1. Ensure `.env` is created from `.env.example`.
2. Execute `docker compose up -d`.
3. The development environment uses `docker-compose.override.yml` for local-friendly settings.

### Production
1. Execute `docker compose -f docker-compose.yml up -d`.
2. Ensure all required environment variables are set in your production host environment or a secure `.env` file.
3. TLS/HTTPS must be terminated at a reverse proxy (like Nginx or Traefik) before reaching `mimir-api`.
4. Resource limits are enforced:
    - `mimir-api`: 1G Memory, 1.0 CPU
    - `mimir-telegram`: 256M Memory, 0.5 CPU
    - `mimir-api` PIDs limit: 256

## Database

### Initial Setup
Apply initial migrations to create the schema:
```bash
dotnet ef database update --project src/Mimir.Infrastructure --startup-project src/Mimir.Api
```

### Connection String Format
Standard PostgreSQL format:
`Host=my-db-host;Port=5432;Database=mimir;Username=mimir;Password=mypassword`

### Migrations
- **Creating**: `dotnet ef migrations add MigrationName --project src/Mimir.Infrastructure --startup-project src/Mimir.Api`
- **Applying**: `dotnet ef database update --project src/Mimir.Infrastructure --startup-project src/Mimir.Api`
- **Rollback**: `dotnet ef database update PreviousMigrationName --project src/Mimir.Infrastructure --startup-project src/Mimir.Api`

## Keycloak Configuration
1. Import the realm configuration from `keycloak/realm-export.json`.
2. Verify the following clients are configured:
    - `mimir-api`: Confidential client for the backend.
    - `mimir-chat`: Public client for the Next.js frontend.
3. Ensure the `admin` and `user` roles are mapped to your target users.

## Health Checks
The API provides a composite health check endpoint at `GET /health`.
- Status `200 OK`: All systems (Database, LiteLLM) are healthy.
- Status `503 Service Unavailable`: One or more systems are degraded.

Integrate this endpoint with your load balancer or orchestrator to manage service availability.

## Monitoring
- **Logs**: Structured JSON logging via Serilog. Inspect via `docker compose logs mimir-api -f`.
- **Tracing**: Correlation IDs are included in all request headers for cross-service tracking.
- **Messaging**: RabbitMQ Management UI is accessible at port `15672`.

## Rollback Procedure
- **Docker Services**: Revert to previous image tags in your deployment script and run `docker compose up -d`.
- **Database**: Use the EF migration rollback command to return the schema to a previous known-good state.
- **Keycloak**: Re-import the verified `realm-export.json` if configuration drift occurs.

## Troubleshooting
- **Database Connection**: Verify `ConnectionStrings__DefaultConnection` and ensure the database container is healthy.
- **Auth Failures**: Check `Jwt__Authority` and ensure Keycloak is reachable from the `mimir-api` container.
- **Messaging Issues**: Verify RabbitMQ connectivity and credentials. Check the Wolverine logs for handler failures.
