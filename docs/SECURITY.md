# Security Guide: nem.Mimir

Mimir implements a multi-layered security model designed for enterprise AI workloads.

## Identity & Authentication

Mimir uses **Keycloak** as its primary Identity Provider (IdP).

- **Standard**: OpenID Connect (OIDC).
- **Protocol**: JWT Bearer Authentication.
- **Workflow**:
  1. Client authenticates with Keycloak.
  2. Client receives a JWT.
  3. Client includes the JWT in the `Authorization` header of API requests.
  4. Mimir API validates the signature, issuer, audience, and expiration.

## Authorization

Authorization is handled through **Role-Based Access Control (RBAC)** and extensible policies.

### Default Roles
- `user`: Can create/manage their own conversations and system prompts.
- `admin`: Full access to user management, audit logs, and plugin lifecycle management.

### Resource Isolation
All data access in the `Application` layer is scoped to the `CurrentUserService.UserId`.
- Users cannot retrieve or modify conversations belonging to other users.
- `Mimir.Infrastructure` repositories include global query filters for soft-deletion and user-scoping where applicable.

## AI-Specific Security

### Prompt Injection Mitigation
Mimir implements multiple layers of defense against prompt injection:
1. **Sanitization**: `SanitizationService` cleans user inputs before they reach the LLM.
2. **System Prompt Enforcement**: Fixed system prompts (immutable once a conversation starts) provide a "root of trust" for model behavior.
3. **Output Sanitization**: Middleware scans assistant responses for sensitive patterns (e.g., API keys, secrets) before sending them to the client.

### Token Limits
To prevent resource exhaustion (DoS) via long AI responses:
- **Global Limits**: Configured in `LiteLlmOptions`.
- **Per-Request Limits**: Enforced by the `LlmService`.

## Sandboxed Code Execution

The `CodeRunner` plugin executes code in a highly restricted environment:
1. **Container Isolation**: Each execution runs in a disposable Docker container.
2. **Network Restricted**: Containers have no access to the internal network or the public internet.
3. **Resource Limits**: CPU and Memory limits are enforced at the Docker level.
4. **Filesystem**: The container filesystem is read-only, except for a temporary workspace.

## API Security

- **Rate Limiting**: Per-user rate limiting (100 requests/min) prevents API abuse.
- **CORS**: Strict Origin validation prevents Cross-Origin Request Forgery.
- **Security Headers**: The following headers are sent with every response:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Content-Security-Policy`: Strict policy allowing only self-connections and SignalR websockets.
- **Correlation IDs**: Every request is tagged with a unique ID for end-to-end tracing in security audits.

## Data Protection

- **Encryption at Rest**: Provided by the underlying PostgreSQL storage (Transparent Data Encryption).
- **Encryption in Transit**: All communication between components (Client -> API, API -> LiteLLM) should occur over TLS 1.3.
- **Soft Deletion**: Accidental data loss is mitigated by a soft-delete policy. Admins can restore deleted entities within a configurable window.

## Audit Logging

The `AuditService` captures all critical security events:
- Successful/Failed Authentication attempts.
- Changes to user roles.
- Conversation creation and deletion.
- Code execution requests (including the code and the result).
- Plugin load/unload actions.

Audit logs are immutable once written to the `AuditLog` table.
