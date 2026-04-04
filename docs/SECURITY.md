---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Security Documentation: nem.Mimir

## Security overview
Security controls are implemented in API middleware/policies, validator pipelines, sanitization services, and tool-governance components.

Primary trust boundaries:
- client -> API/SignalR
- API -> LiteLLM proxy
- API/Sync -> RabbitMQ
- API -> PostgreSQL
- API -> MCP servers

## Authentication and authorization
- Authentication modes:
  - Keycloak-based auth via shared contracts integration.
  - `StandaloneMode` development handler for local admin-only setup.
- Authorization policies in `Program.cs`:
  - `RequireAdmin`
  - `RequireUser`
- MCP server management endpoints are admin-only.

## Transport and perimeter controls
- CORS policy with configured origin allowlist.
- Security headers appended in middleware:
  - `X-Content-Type-Options`
  - `X-Frame-Options`
  - CSP and related headers
- HSTS + HTTPS redirection enabled outside development.

## Input validation and sanitization
Validation layers:
- FluentValidation behavior in MediatR pipeline.
- Endpoint-specific validators for conversations/messages/prompts/OpenAI requests.

Sanitization layers:
- `SanitizationService` strips dangerous tags, handlers, and common prompt-injection markers.
- `OutputSanitizationMiddleware` logs suspicious patterns on message-related routes.

## Rate limiting and abuse prevention
- Fixed-window per-user limiter: 100 requests per minute.
- SignalR max receive message size limited (256 KB).
- Kestrel request body max size set to 10 MB.

## Tool and MCP security
- Tool access follows default-deny whitelist per MCP server.
- Tool execution is audited (`McpToolAuditLog`) with latency, success/failure, and payload metadata.
- `McpToolProvider` re-validates whitelist at execution time (defense in depth).

## Sandbox execution controls
Code execution endpoint delegates to sandbox service with Docker isolation assumptions and constrained execution profile.
Repository infrastructure and compose config enforce non-root/read-only/resource-limited patterns for related workers.

## Data protection posture
- User and conversation access is owner-scoped in handlers.
- Soft-delete and audit metadata preserve operational traceability.
- Secrets are expected from environment/OpenBao integration path; not hardcoded in handlers.

## Incident-relevant observability
- Correlation ID middleware enables request traceability.
- Structured logging via Serilog.
- Health report emitter publishes service health metrics through message bus.

## STRIDE snapshot
| Threat | Primary mitigation in code |
| :--- | :--- |
| Spoofing | JWT auth, policy checks |
| Tampering | validators + sanitization + controlled persistence |
| Repudiation | audit entries + event publication + correlation IDs |
| Information disclosure | ownership checks + admin policy boundaries |
| Denial of service | rate limiting + body/message size limits |
| Elevation of privilege | role-based policies + protected admin controllers |

## Known limitations
- Legacy branch identity model still depends on primitive IDs in several paths.
- Security posture differs between standalone mode and production auth mode; standalone is for local development only.

## Cross-references
- [COMPLIANCE](./COMPLIANCE.md)
- [AI](./AI.md)
- [QA](./QA.md)
- [INFRASTRUCTURE](./INFRASTRUCTURE.md)
