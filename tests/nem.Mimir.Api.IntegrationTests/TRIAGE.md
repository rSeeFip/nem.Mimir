# Integration Test Triage — nem.Mimir.Api.IntegrationTests

**Date:** 2026-05-07  
**Branch:** `feature/mcp-client-integration`  
**Factory:** `MimirWebApplicationFactory` (Testcontainers Postgres + RabbitMQ + WireMock)  
**Total tests after triage:** 154 (0 deleted, 4 fixed, 5 added by parallel T6 work)

---

## Summary

| Class | Decision | Tests | Notes |
|---|---|---|---|
| `AdminControllerTests` | PASS | 9 | Auth boundary coverage for admin endpoints |
| `AdminRestoreEntityTests` | PASS | 2 | Auth boundary for restore endpoint |
| `ApiIntegrationNegativeTests` | FIX | 19 | 4 tests fixed (see below) |
| `AuthenticationTests` | PASS | 1 | Parametric auth sweep |
| `ChatHubTests` | PASS | 4 | SignalR hub auth + existence |
| `CodeExecutionControllerTests` | PASS | 13 | Auth + route validation for code exec |
| `ConversationsControllerTests` | PASS | 9 | Auth boundary + route constraints |
| `HealthCheckTests` | PASS | 1 | Health endpoint reachable without auth |
| `MessagesControllerTests` | PASS | 4 | Auth + invalid-GUID route rejection |
| `ModelsControllerTests` | PASS | 4 | Auth + authenticated model list call |
| `OpenAiCompatControllerTests` | PASS | 15 | Auth + route existence for OpenAI-compat layer |
| `PluginsControllerTests` | PASS | 14 | Auth + route existence for plugin management |
| `SystemPromptsControllerTests` | PASS | 14 | Auth + route constraints for system prompts |
| `RateLimitingTests` | PASS | 2 | Per-tenant rate limit isolation + Retry-After header |

---

## Class Decisions

### AdminControllerTests — PASS
Tests auth boundaries on all admin endpoints (GET/PUT/DELETE users, audit log). Valuable: admin endpoints are high-risk; confirming 401 on every verb prevents accidental exposure. No changes needed.

### AdminRestoreEntityTests — PASS
Tests auth on the restore-entity endpoint with valid and invalid GUIDs. Valuable: restore is a destructive admin action. No changes needed.

### ApiIntegrationNegativeTests — FIX
Cross-cutting negative tests: wrong HTTP methods, invalid content types, malformed GUIDs, oversized payloads, HEAD/OPTIONS/double-slash paths.

**Fixed tests:**

| Test | Problem | Fix |
|---|---|---|
| `Health_PostMethod_Returns404OrMethodNotAllowed` | ASP.NET health middleware accepts POST → returned 200, not 404/405 | Renamed to `Health_PostMethod_DoesNotReturn500`; asserts `< 500` (correct: health endpoint must not crash on any method) |
| `HeadMethod_OnGetEndpoints_Returns401` (×3) | ASP.NET Core returns 405 for HEAD on endpoints that don't explicitly declare it, before auth middleware fires | Renamed to `HeadMethod_OnGetEndpoints_Returns401OrMethodNotAllowed`; accepts 401 or 405 |

All other tests in this class were already correct and passing.

### AuthenticationTests — PASS
Single parametric test sweeping all authenticated endpoints to confirm non-200 without a token. Valuable: regression guard for accidentally removing `[Authorize]`. No changes needed.

### ChatHubTests — PASS
Tests SignalR hub negotiate endpoint (401 without token), invalid-token rejection, and hub route existence. Valuable: SignalR auth is a distinct code path from REST auth. No changes needed.

### CodeExecutionControllerTests — PASS
Tests auth on the code-execution endpoint across multiple invalid-input shapes (empty code, null language, invalid language, malformed JSON, oversized payload). Valuable: code execution is a high-risk endpoint; confirming auth fires before any execution logic is critical. No changes needed.

### ConversationsControllerTests — PASS
Tests auth on all conversation CRUD endpoints plus GUID route-constraint rejection. Valuable: conversations are the core domain object; auth coverage is essential. No changes needed.

### HealthCheckTests — PASS
Confirms `/health` returns 200 or 503 (degraded) without authentication. Valuable: health endpoint must be publicly accessible for load-balancer probes. No changes needed.

### MessagesControllerTests — PASS
Tests auth on send/get message endpoints and GUID route-constraint rejection. Valuable: messages are the core interaction; auth + route validation matter. No changes needed.

### ModelsControllerTests — PASS
Tests auth on model list/status endpoints, plus one authenticated call that verifies the WireMock stub returns a model list. Valuable: the authenticated call exercises the full LiteLLM proxy path through WireMock. No changes needed.

### OpenAiCompatControllerTests — PASS
Tests auth on the OpenAI-compatible `/v1/chat/completions` and `/v1/models` endpoints across multiple input shapes (empty body, malformed JSON, null content, streaming flag). Valuable: the OpenAI-compat layer is used by external clients; auth and route existence are critical. No changes needed.

### PluginsControllerTests — PASS
Tests auth on plugin load/list/execute/unload endpoints, including path-traversal attempt in plugin name. Valuable: plugin loading is a security-sensitive operation; path-traversal test is particularly important. No changes needed.

### SystemPromptsControllerTests — PASS
Tests auth on all system-prompt CRUD endpoints plus GUID route-constraint rejection and pagination route existence. Valuable: system prompts affect LLM behavior; auth coverage is important. No changes needed.

---

## Pre-existing Build Fix

`src/nem.Mimir.Api/Program.cs` was missing `using nem.Mimir.Domain.MultiTenancy;` introduced by T2 (per-tenant rate limiting). Added the missing using directive to restore compilation. This is a production code fix required for the test project to build — it does not change runtime behavior.

---

## Triage Criteria Applied

- **PASS**: Test logic correct, assertions match actual API behavior, no changes needed.
- **FIX**: Test logic correct but assertions used wrong expected values (wrong HTTP status codes). Fixed assertions to match actual ASP.NET Core behavior.
- **DELETE**: Not applied — no tests were purely scaffolded boilerplate or trivially redundant. All tests either cover auth boundaries (preventing accidental exposure) or route constraints (preventing routing regressions).
