---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Business Logic Documentation: nem.Mimir

## Purpose
`nem.Mimir` implements conversational workflows for authenticated users across web, terminal, and bot channels, with model inference, prompt governance, and optional tool execution.

## Core rule domains
1. Conversation lifecycle management.
2. Message submission and assistant response generation.
3. Prompt template management and rendering.
4. Model catalog discovery and status checks.
5. Plugin and MCP tool governance.
6. Administrative operations and restoration of soft-deleted entities.

## Rule inventory (code-mapped)
- **Ownership rule**: only conversation owner can view/send/update/archive/delete (`SendMessageCommand`, conversation queries).
- **Archived conversation rule**: cannot add messages once archived (`Conversation.AddMessage`).
- **Input bounds rule**: validators enforce model name patterns, content max length, token-related ranges.
- **Prompt-template rule**: prompt name/template cannot be empty (`SystemPrompt` + validators).
- **Tool-loop rule**: max 5 tool iterations in `SendMessageCommand`.
- **MCP privilege rule**: MCP admin endpoints require `RequireAdmin` policy.

## Main workflows
### Create conversation
1. API receives `CreateConversationRequest`.
2. Command handler resolves current user.
3. Domain creates `Conversation` with active status and default settings.
4. Repository persists aggregate.

### Send message (native API)
1. Request validation for content/model constraints.
2. Ownership verification for target conversation.
3. Context window built from system prompt + history + new message.
4. User message is appended.
5. If tools are available, run tool loop; else direct stream path.
6. Assistant message is persisted with estimated token count.

### OpenAI-compatible chat completion
1. `/v1/chat/completions` validates OpenAI-like payload.
2. Streaming path emits SSE chunks and `[DONE]` marker.
3. Non-stream path uses `CreateChatCompletionCommand`.
4. Chat lifecycle events are published to Wolverine.

### System prompt rendering
1. Prompt template retrieved by ID.
2. `SystemPromptService.RenderTemplate` substitutes `{{variable}}` tokens.
3. Rendered output returned without mutating template data.

## Messaging semantics
- Event publisher methods include:
  - `PublishChatRequestAsync`
  - `PublishChatCompletedAsync`
  - `PublishMessageSentAsync`
  - `PublishConversationCreatedAsync`
  - `PublishAuditEventAsync`
- Delivery guarantees rely on Wolverine durable inbox/outbox.

## Tool invocation semantics
- Tools are exposed from plugins and MCP servers via composite provider.
- MCP tools are whitelist-filtered and collision-handled.
- Tool failures are converted to content payloads and fed back into model context.

## Validation and error behavior
- FluentValidation pipelines run before handlers.
- Domain invariants throw early on invalid state transitions.
- API-level exceptions are mapped to ProblemDetails-style responses by global exception handling.

## Business rule matrix
| Rule ID | Description | Enforced By | Severity |
| :--- | :--- | :--- | :--- |
| BR-001 | User can access only own conversations | Conversation handlers + user context | Critical |
| BR-002 | Archived conversations are immutable for new messages | `Conversation.AddMessage` | High |
| BR-003 | Prompt and message payloads must pass injection-aware validation | API validators + sanitization | Critical |
| BR-004 | Tool execution recursion capped to avoid unbounded loops | `SendMessageCommand.MaxToolIterations` | High |
| BR-005 | MCP configuration changes must be admin-only | `McpServersController` + policy | High |
| BR-006 | Soft-deleted entities can only be restored via admin flow | `AdminController` restore command | Medium |

## Operationally relevant logic gaps
- Defaults are not fully aligned (`ConversationSettings.Default` vs runtime model defaults).
- Reasoner-agent memory path is in-memory, not persistent document storage.

## Cross-references
- [AI](./AI.md)
- [ARCHITECTURE](./ARCHITECTURE.md)
- [DATA-MODEL](./DATA-MODEL.md)
- [QA](./QA.md)
