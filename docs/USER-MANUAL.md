---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# User Manual: nem.Mimir

## Audience
This manual targets:
- application users consuming conversational APIs/UI,
- operators administering prompts/plugins/MCP endpoints,
- support engineers troubleshooting chat and model behavior.

## Prerequisites
- Valid authentication token (unless standalone local mode).
- Reachable API host.
- At least one available LiteLLM model for successful completions.

## Core user flows
### 1) Create a conversation
Endpoint: `POST /api/conversations`

Required input:
- `title`

Optional input:
- `systemPrompt`
- `model`

Expected outcome:
- new conversation resource returned with identifier.

### 2) Send a message and receive response
Endpoint: `POST /api/conversations/{conversationId}/messages`

Input:
- `content`
- optional `model`

Behavior:
- system prompt and history are included automatically.
- assistant response is generated and persisted.

### 3) OpenAI-compatible completion
Endpoint: `POST /v1/chat/completions`

Supports:
- non-streaming JSON responses
- streaming SSE responses (`stream=true`)

### 4) Manage system prompts
Endpoints under `api/systemprompts` support create/list/get/update/delete/render.
Use render endpoint to verify template variables before operational use.

### 5) Inspect model availability
Use:
- `GET /api/models`
- `GET /api/models/{modelId}/status`
- `GET /v1/models` (OpenAI-compatible)

### 6) Admin-only operations
Admin users can:
- manage users and roles
- inspect audit logs
- restore soft-deleted entities
- configure MCP servers and tool whitelists

## Troubleshooting
### 401/403 responses
- validate token and role claims.
- check whether endpoint requires `RequireAdmin`.

### 404 conversation not found
- confirm conversation ID belongs to authenticated user.

### completion failures
- check LiteLLM reachability and model availability.
- inspect `/health` and logs for upstream failures.

### tool/MCP issues
- ensure MCP server config is enabled and connected.
- verify tool is explicitly whitelisted.
- review MCP tool audit logs for error details.

## Operational guardrails
- Do not run admin endpoints with user tokens.
- Avoid relying on standalone auth mode outside local development.
- Treat this repository as legacy branch; validate behavior against active typed-ID branch for migration planning.

## FAQ
**Q: Which model is used by default?**
A: It depends on call path; `SendMessageCommand` defaults to `phi-4-mini`, while other defaults are configuration-driven.

**Q: Why does history sometimes truncate?**
A: Context window service trims oldest messages to fit model token limits.

**Q: Can the model invoke tools automatically?**
A: Yes, when tool definitions are available and whitelisted, the tool loop executes calls and feeds results back.

## Glossary
- **LiteLLM**: OpenAI-compatible proxy used by backend inference adapter.
- **MCP**: Model Context Protocol endpoints for external tools/resources/prompts.
- **Tool whitelist**: per-server allowlist that gates executable tools.
- **Context window**: bounded message history sent to the model.
- **Standalone mode**: local dev auth mode that bypasses Keycloak.

## Cross-references
- [AI](./AI.md)
- [ARCHITECTURE](./ARCHITECTURE.md)
- [SECURITY](./SECURITY.md)
- [INFRASTRUCTURE](./INFRASTRUCTURE.md)
