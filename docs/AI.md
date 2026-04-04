---
repo: nem.Mimir
tier: tier-1
status: regenerated
last_verified_against: src/* (legacy branch)
---

# AI Documentation: nem.Mimir

## Scope and branch status
`nem.Mimir` is the legacy conversational AI service. The active refactor branch is `nem.Mimir-typed-ids`, but this document intentionally describes the canonical legacy repository.

The core AI path is implemented in:
- `src/nem.Mimir.Api/Controllers/OpenAiCompatController.cs`
- `src/nem.Mimir.Application/Conversations/Commands/SendMessage.cs`
- `src/nem.Mimir.Infrastructure/LiteLlm/LiteLlmClient.cs`
- `src/nem.Mimir.Infrastructure/Services/ContextWindowService.cs`

## LLM routing architecture (code-verified)
Mimir routes all model inference through `ILlmService`, implemented by `LiteLlmClient`.

Routing layers:
1. **API transport layer**: `/v1/chat/completions` accepts OpenAI-compatible requests.
2. **Application command layer**: `CreateChatCompletionCommand` and `SendMessageCommand` build provider-agnostic `LlmMessage` lists.
3. **Provider adapter layer**: `LiteLlmClient` converts domain messages to OpenAI-compatible JSON for LiteLLM.
4. **Provider proxy layer**: LiteLLM forwards to concrete models declared in `litellm_config.yaml`.

No direct OpenAI/Azure/Anthropic SDK is used in backend runtime paths; provider switching is proxy-mediated.

## Inference pipeline
### Non-streaming pipeline
- Entry: `OpenAiCompatController.HandleNonStreamingResponse`
- Command: `CreateChatCompletionCommand`
- Execution: `ILlmService.SendMessageAsync`
- Return: `ChatCompletionResult` with usage (`prompt_tokens`, `completion_tokens`, `total_tokens`)

### Streaming pipeline
- Entry: `OpenAiCompatController.HandleStreamingResponse` (SSE)
- Execution: `ILlmService.StreamMessageAsync`
- Parsing: `SseStreamParser`
- Behavior: emits chunked `ChatCompletionChunk`, then `[DONE]`

### Conversation command pipeline
- Entry: `MessagesController.Send`
- Command: `SendMessageCommand`
- Context assembly: `ContextWindowService.BuildLlmMessagesAsync`
- Branching:
  - if no tools: `StreamResponseAsync`
  - if tools available: `ExecuteToolLoopAsync` with max 5 iterations

## Tool-calling and MCP orchestration
`SendMessageCommand` supports function/tool calling via `ToolDefinition` and `LlmToolCall`.

Execution model:
- `ILlmService.SendMessageAsync(model, messages, tools)` may return `ToolCalls`.
- Application executes tools through `IToolProvider`.
- Tool outputs are appended as `role="tool"` messages and re-submitted to the model.

Provider composition is in `Infrastructure/DependencyInjection.cs`:
- `PluginToolProvider` (legacy plugin tools)
- `McpToolProvider` (connected MCP servers)
- `CompositeToolProvider` + `AuditingToolProviderDecorator`

MCP controls are implemented through:
- `McpClientManager` (stdio/sse/streamable-http transport)
- `McpClientStartupService` (auto-connect enabled configs)
- `McpConfigChangeListener` + `McpConfigChangeHandler` (runtime refresh)
- whitelist enforcement and audit logging for tool execution

## Prompt management
Prompt entities and services:
- Domain: `SystemPrompt` (name/template/default/active)
- Repository: `SystemPromptRepository`
- API: `SystemPromptsController`
- Rendering: `SystemPromptService.RenderTemplate` (regex substitution)

Runtime prompt resolution for chat context:
- `ContextWindowService` resolves default prompt from DB (`GetDefaultAsync`)
- fallback constant: `"You are Mimir, a helpful AI assistant."`

Prompt protections:
- API validators (`OpenAiCompatValidators`, `MessageValidators`, `ConversationValidators`) include prompt injection guards.
- `SanitizationService` strips known instruction-hijack markers and dangerous HTML/script patterns.

## Conversation context handling
Main context strategy for LLM requests:
- Merge order in `ContextWindowService`:
  1. system message
  2. historical conversation messages (ordered by `CreatedAt`)
  3. current user message
- Token budgeting by character heuristic `(len + 3) / 4`
- Oldest history is trimmed until model token limit is satisfied.

Model token limits in code:
- `phi-4-mini`: 16,384
- `qwen-2.5-72b`: 131,072
- `qwen-2.5-coder-32b`: 131,072

## Model selection strategy
Selection happens at multiple layers:
- Request-level: caller can provide `model`.
- Application default: `SendMessageCommand` uses `phi-4-mini` when unspecified.
- LiteLLM adapter default: `LiteLlmClient` falls back to configured `LiteLlmOptions.DefaultModel`.
- Context trimming uses `ContextWindowService.GetTokenLimit(model)`.

Declared model catalogs currently diverge:
- `LlmModels` constants still reference qwen-2.5 names.
- `litellm_config.yaml` defines qwen3/glm model names (including `glm-4.6v-flash`) plus embeddings.

This mismatch is relevant for operational documentation and should be kept explicit.

## Safety and governance controls in the AI path
- Authentication + authorization on all chat endpoints.
- Rate limiting (`per-user`, 100/min).
- Input validation via MediatR `ValidationBehavior` and endpoint validators.
- Output and input sanitization via `SanitizationService` and `OutputSanitizationMiddleware` logging.
- Tool execution audit (`McpToolAuditLog`) and whitelist checks.

## Known architectural boundaries
- Legacy `nem.Mimir` still uses primitive IDs in many entities.
- `ConversationSettings.Default` still defaults model to `gpt-4`, while runtime defaults elsewhere are LiteLLM model IDs.
- Agent-side memory service (`ConversationMemoryService`) is in-memory despite comments referencing Marten-style persistence.

## Cross-references
- [ARCHITECTURE](./ARCHITECTURE.md)
- [BUSINESS-LOGIC](./BUSINESS-LOGIC.md)
- [DATA-MODEL](./DATA-MODEL.md)
- [SECURITY](./SECURITY.md)
- [QA](./QA.md)
