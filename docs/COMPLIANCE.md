---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Compliance Documentation: nem.Mimir

## Scope
This document describes implemented controls in the legacy `nem.Mimir` service. It does not claim controls that are not present in code/config.

In-scope surfaces:
- authenticated API and SignalR chat endpoints
- persistence of conversations/messages/users/prompts/MCP telemetry
- audit and admin operations
- sandboxed code execution integration

## Standards mapping (implemented evidence)
| Control area | Framework mapping | Implementation artifact |
| :--- | :--- | :--- |
| Authenticated access | GDPR Art. 32 / SOC2 CC6 | JWT auth setup + policies in `Program.cs` and auth extensions |
| Input validation | SOC2 CC7 | FluentValidation pipeline + endpoint validators |
| Security event logging | SOC2 CC7 / ISO 27001 A.12 | `AuditEntry`, `AuditService`, Wolverine audit events |
| Least privilege admin actions | SOC2 CC6 | `RequireAdmin` policy on admin and MCP configuration controllers |
| Tool governance | SOC2 CC8 | MCP whitelist + tool audit log entities/services |
| Data lifecycle controls | GDPR Art. 5/17 | soft-delete with restore command paths |

## Data governance
Data classes processed by service logic:
- conversational text and prompt content
- user identity metadata from JWT claims
- administrative audit records
- MCP tool input/output telemetry

Governance measures implemented:
- user-scoped access checks in conversation/message handlers
- soft-delete flags and query filters for major entities
- admin-only restoration endpoints for controlled recovery

## Auditability and evidence
Evidence-producing components:
- `AuditEntry` persistence model
- `MimirEventPublisher` + `AuditEventHandler`
- request correlation middleware for traceability linkage
- MCP tool audit logger (`McpToolAuditLog`)

## Retention and erasure posture
Current behavior in code:
- soft-delete used for `Conversation`, `User`, `SystemPrompt`.
- restore endpoint enables reversibility for administrator-led recovery.

What is not implemented as explicit policy code here:
- fixed retention scheduler with legal retention windows.
- automated data-subject request workflow orchestration.

## Third-party and dependency posture
Operational dependencies with compliance relevance:
- Keycloak (identity)
- PostgreSQL (primary data store)
- RabbitMQ (message transport)
- LiteLLM and upstream model providers

A separate dependency license/vulnerability note previously existed (`compliance-audit.md`), but this regeneration focuses on repository-native control behavior.

## Risk register summary
| Risk | Residual level | Existing mitigation |
| :--- | :--- | :--- |
| Prompt/data leakage via malicious input | Medium | validators + sanitization + auth boundaries |
| Excessive tool privileges via MCP | Medium | default-deny whitelist and audit logging |
| Legacy model/config drift | Medium | explicit defaults and runtime model checks |
| Soft-delete misuse / undeleted sensitive data | Medium | admin restore controls + audit trail |

## Verification checkpoints
- Validate auth policy on all privileged endpoints.
- Validate audit entries created for admin actions and tool execution.
- Validate conversation ownership and isolation tests in API/E2E suites.
- Validate sanitizer and validator negative tests remain passing.

## Document governance
This compliance snapshot is tied to code in `nem.Mimir` legacy branch and should be refreshed whenever:
- MCP policy behavior changes,
- auth policy wiring changes,
- soft-delete/restore behavior changes,
- new data classes are introduced.

## Cross-references
- [SECURITY](./SECURITY.md)
- [DATA-MODEL](./DATA-MODEL.md)
- [QA](./QA.md)
- [AI](./AI.md)
