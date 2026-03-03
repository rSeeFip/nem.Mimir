# API Reference: nem.Mimir

The Mimir API is a RESTful service that provides endpoints for managing conversations, messages, system prompts, models, and administrative tasks. It also hosts an OpenAI-compatible gateway.

## Authentication

All endpoints (except `/health`) require an **OpenID Connect (OIDC)** JWT Bearer token.
Header: `Authorization: Bearer <token>`

## Rate Limiting

The API implements a "per-user" rate limit:
- **Limit**: 100 requests per minute.
- **Window**: 1 minute fixed window.
- **Status Code**: 429 Too Many Requests.

---

## Conversations

### Create Conversation
`POST /api/conversations`
- **Auth**: Required
- **Body**:
  ```json
  {
    "title": "string",
    "systemPrompt": "string (optional)",
    "model": "string (optional)"
  }
  ```
- **Returns**: `201 Created` with `ConversationDto`

### List Conversations
`GET /api/conversations`
- **Auth**: Required
- **Query**: `pageNumber=1`, `pageSize=20`
- **Returns**: `200 OK` with `PaginatedList<ConversationListDto>`

### Get Conversation Detail
`GET /api/conversations/{id}`
- **Auth**: Required
- **Returns**: `200 OK` with `ConversationDto`

### Update Title
`PUT /api/conversations/{id}/title`
- **Auth**: Required
- **Body**: `{"title": "new title"}`
- **Returns**: `204 No Content`

### Archive Conversation
`POST /api/conversations/{id}/archive`
- **Auth**: Required
- **Returns**: `204 No Content`

### Delete Conversation
`DELETE /api/conversations/{id}`
- **Auth**: Required
- **Returns**: `204 No Content`

---

## Messages

### Send Message
`POST /api/conversations/{conversationId}/messages`
- **Auth**: Required
- **Body**:
  ```json
  {
    "content": "string",
    "model": "string (optional)"
  }
  ```
- **Returns**: `201 Created` with `MessageDto`

### Get Message History
`GET /api/conversations/{conversationId}/messages`
- **Auth**: Required
- **Query**: `pageNumber=1`, `pageSize=20`
- **Returns**: `200 OK` with `PaginatedList<MessageDto>`

---

## OpenAI-Compatible Gateway

Mimir provides a drop-in replacement for OpenAI at the `/v1` prefix.

### Chat Completions
`POST /v1/chat/completions`
- **Auth**: Required
- **Format**: [OpenAI Chat Completion API](https://platform.openai.com/docs/api-reference/chat/create)
- **Streaming**: Supports `stream: true` using SSE.
- **Returns**: `200 OK` with `ChatCompletionResult` or `text/event-stream`

### List Models
`GET /v1/models`
- **Auth**: Required
- **Returns**: `200 OK` with `OpenAiModelsResponse`

---

## Models

### List Available Models
`GET /api/models`
- **Auth**: Required
- **Returns**: `200 OK` with `IReadOnlyList<LlmModelInfoDto>`

### Get Model Status
`GET /api/models/{modelId}/status`
- **Auth**: Required
- **Returns**: `200 OK` with `LlmModelInfoDto`

---

## System Prompts

### Create System Prompt
`POST /api/systemprompts`
- **Auth**: Required
- **Body**:
  ```json
  {
    "name": "string",
    "template": "string with {{variable}}",
    "description": "string (optional)"
  }
  ```
- **Returns**: `201 Created`

### Render Prompt
`POST /api/systemprompts/{id}/render`
- **Auth**: Required
- **Body**: `{"variables": {"name": "val"}}`
- **Returns**: `200 OK` with `{"renderedText": "..."}`

---

## Code Execution

### Execute Code
`POST /api/conversations/{conversationId}/code-execution`
- **Auth**: Required
- **Body**:
  ```json
  {
    "language": "python|javascript",
    "code": "print('hello')"
  }
  ```
- **Returns**: `200 OK` with `CodeExecutionResultDto` (stdout, stderr, exitCode, duration)

---

## Plugins

### List Plugins
`GET /api/plugins`
- **Auth**: Required
- **Returns**: `200 OK` with `IReadOnlyList<PluginMetadata>`

### Load Plugin (Admin Only)
`POST /api/plugins`
- **Auth**: `RequireAdmin`
- **Body**: `{"assemblyPath": "/path/to/plugin.dll"}`

---

## Admin

### List Users
`GET /api/admin/users`
- **Auth**: `RequireAdmin`
- **Returns**: `PaginatedList<UserDto>`

### Audit Log
`GET /api/admin/audit`
- **Auth**: `RequireAdmin`
- **Query**: `userId`, `action`, `from`, `to`
- **Returns**: `PaginatedList<AuditEntryDto>`

### Restore Deleted Entity
`POST /api/admin/restore/{entityType}/{id}`
- **Auth**: `RequireAdmin`
- **Returns**: `204 No Content`
