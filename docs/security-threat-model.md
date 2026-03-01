# Security Threat Model: Docker & Container Isolation

## Document Metadata
- **Status**: Active
- **Last Updated**: 2026-03-01
- **Scope**: nem.Mimir container security posture

---

## 1. Overview

nem.Mimir runs as a multi-container application orchestrated via Docker Compose.
This document describes the threat model for container isolation, particularly
the code execution sandbox (`mimir-sandbox`) and long-running services.

## 2. Container Inventory

| Container        | Purpose                    | Network Access | Privileges    |
|------------------|----------------------------|----------------|---------------|
| mimir-api        | ASP.NET API server         | mimir-network  | Non-root      |
| mimir-chat       | Next.js frontend           | mimir-network  | Non-root      |
| mimir-telegram   | Telegram bot service       | mimir-network  | Non-root, read_only |
| mimir-sandbox    | User code execution        | **none**       | Non-root, read_only, seccomp |
| mimir-db         | PostgreSQL database        | mimir-network  | postgres user |
| mimir-keycloak   | Identity provider          | mimir-network  | Non-root      |
| mimir-litellm    | LLM proxy                  | mimir-network  | Non-root      |
| mimir-rabbitmq   | Message broker             | mimir-network  | Non-root      |

## 3. Sandbox Threat Model (mimir-sandbox)

### 3.1 Design

The sandbox executes user-submitted code in an ephemeral container with maximum
isolation. Each execution creates a new container with the following constraints:

- **Network**: `network_mode: none` — no network access whatsoever
- **Filesystem**: `read_only: true` with `tmpfs /tmp` (100 MB limit)
- **Memory**: Hard limit at 512 MB
- **CPU**: Limited to 1 core
- **Process IDs**: `pids_limit: 128` — prevents fork bombs
- **User**: Runs as unprivileged `sandbox` user
- **Seccomp**: Custom profile at `docker/sandbox/seccomp-profile.json`

### 3.2 Threat Matrix

| Threat                        | Mitigation                                         | Residual Risk |
|-------------------------------|-----------------------------------------------------|---------------|
| Container escape              | Seccomp profile restricts syscalls; non-root user   | Low           |
| Fork bomb / resource exhaust  | pids_limit=128, memory=512MB, cpus=1                | Low           |
| Network exfiltration          | network_mode=none                                   | Very Low      |
| Filesystem persistence        | read_only=true, ephemeral containers                | Very Low      |
| Disk space exhaustion         | tmpfs /tmp capped at 100 MB                         | Low           |
| Privilege escalation          | Non-root sandbox user, no capabilities added        | Low           |
| Host Docker socket access     | Socket NOT mounted into sandbox containers          | Very Low      |

### 3.3 Docker Socket

**The Docker socket (`/var/run/docker.sock`) is NOT exposed to any application
container.** The `SandboxService` in `mimir-api` communicates with Docker Engine
via the socket mounted on the host, not from within containers. This is a
standard pattern but requires the API host to have Docker access.

**Recommendation**: In production, consider using a rootless Docker daemon or
a dedicated container runtime (gVisor, Kata Containers) for additional isolation.

## 4. Service Container Hardening

### 4.1 Applied Hardening

All long-running service containers include:

- **Resource limits**: Memory and CPU caps via `deploy.resources.limits`
- **PID limits**: `pids_limit` on API (256) and Telegram (128) containers
- **Health checks**: All services have health check endpoints
- **Restart policy**: `unless-stopped` for resilience
- **Read-only filesystem**: Applied to `mimir-telegram`
- **Environment variable secrets**: All credentials use `${VAR:-default}` pattern

### 4.2 Recommendations for Production

1. **Remove default passwords**: All `:-changeme` and `:-guest` defaults must be
   replaced with strong, unique secrets via `.env` file or secret manager
2. **Enable TLS**: Add TLS termination (reverse proxy or Kestrel HTTPS)
3. **Network segmentation**: Split `mimir-network` into frontend/backend tiers
4. **Log aggregation**: Centralize logs (already partially done via Serilog)
5. **Image scanning**: Add container image vulnerability scanning to CI/CD
6. **Rootless Docker**: Consider rootless mode for the Docker daemon in production
7. **Secret management**: Integrate HashiCorp Vault or cloud KMS for credential rotation

## 5. Credential Flow

```
.env file (or CI/CD secrets)
  │
  ├─► docker-compose.yml (${VAR:-default} interpolation)
  │     ├─► mimir-db: POSTGRES_PASSWORD
  │     ├─► mimir-api: ConnectionStrings, RabbitMQ, Keycloak
  │     ├─► mimir-keycloak: KEYCLOAK_ADMIN_PASSWORD, KC_DB_PASSWORD
  │     ├─► mimir-telegram: TELEGRAM_BOT_TOKEN, ConnectionStrings
  │     └─► mimir-rabbitmq: RABBITMQ_DEFAULT_USER/PASS
  │
  └─► appsettings.Production.json (${VAR} placeholders for host deployment)
```

**Development**: Default values in docker-compose.yml and appsettings.Development.json
**Production**: All secrets via environment variables or secret manager — no defaults

## 6. Change Log

| Date       | Change                                              | Author |
|------------|-----------------------------------------------------|--------|
| 2026-03-01 | Initial threat model creation (Wave 1+2 security)   | nem.Mimir team |
