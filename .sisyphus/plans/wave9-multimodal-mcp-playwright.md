# ADR-005: Wave 9 — Multimodal LLM & MCP Playwright Integration

## Status
**Proposed** — Pending review

## Date
2026-03-02

## Context

Mimir's LLM integration currently handles **text-only** messages. All message models across the stack (Domain `Message.Content: string`, Application DTOs, Infrastructure `ChatMessageRequest.Content: string`, API `ChatMessage.Content: string`, Frontend `Message.content: string`) use plain strings. The LiteLLM proxy models (`qwen-2.5-72b`, `phi-4-mini`, `qwen-2.5-coder-32b`) running on LM Studio are all text-only models.

The user requires:
1. **Multimodal LLM support** — The LLM must be able to process images/screenshots (not just text)
2. **MCP Playwright integration** — Browser automation with screenshot capabilities, surfaced to the LLM via MCP (Model Context Protocol)
3. Both are **mandatory** requirements

A separate `nem.MCP` project exists with its own MCP server infrastructure and plugins (HolisticWorld, HomeAssistant, KnowHub, LlmMcpServer). This plan must integrate MCP tooling into nem.Mimir's existing plugin/chat architecture.

## Problem Analysis

### Gap 1: Text-Only Message Pipeline
The OpenAI multimodal API uses a `content` array format:
```json
{
  "role": "user",
  "content": [
    {"type": "text", "text": "What's in this screenshot?"},
    {"type": "image_url", "image_url": {"url": "data:image/png;base64,..."}}
  ]
}
```
Mimir uses `content: string` everywhere. This must become a polymorphic type (string OR array) across all 4 layers.

### Gap 2: No Tool/Function Calling
Mimir's `ChatCompletionRequest` has no `tools`, `tool_choice`, or `function_call` fields. The LLM cannot invoke plugins proactively — plugins are currently triggered manually by the user. For MCP Playwright to work, the LLM must be able to call tools autonomously.

### Gap 3: No Vision-Capable Model
None of the 4 configured LM Studio models support vision input. A multimodal model must be added (e.g., `Qwen2.5-VL`, `LLaVA`, `InternVL2`).

### Gap 4: No MCP Client in Mimir
nem.MCP has MCP server infrastructure, but Mimir has no MCP client to connect to MCP servers and expose their tools to the LLM.

### Gap 5: No File Upload Pipeline
No image upload, storage, or attachment handling exists in Mimir (no endpoints, no storage, no domain model).

## Decision

### Architecture: 5 Workstreams

```
┌─────────────────────────────────────────────────────────────────┐
│                     WAVE 9 ARCHITECTURE                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────┐   ┌───────────┐   ┌──────────────┐              │
│  │ Frontend  │──▶│  API Hub  │──▶│ Application  │              │
│  │ (upload   │   │ (SignalR)  │   │ (SendMessage │              │
│  │  images)  │   │            │   │  + tools)    │              │
│  └──────────┘   └───────────┘   └──────┬───────┘              │
│                                         │                       │
│                          ┌──────────────┼──────────────┐       │
│                          ▼              ▼              ▼       │
│                   ┌────────────┐ ┌───────────┐ ┌──────────┐   │
│                   │ LiteLLM    │ │ MCP Client│ │ Plugin   │   │
│                   │ (multimodal│ │ (connects │ │ Manager  │   │
│                   │  content)  │ │  to MCP   │ │ (legacy) │   │
│                   └────────────┘ │  servers) │ └──────────┘   │
│                                  └─────┬─────┘                 │
│                                        ▼                       │
│                                 ┌──────────────┐              │
│                                 │ MCP Playwright│              │
│                                 │ Server        │              │
│                                 │ (screenshots, │              │
│                                 │  DOM, clicks) │              │
│                                 └──────────────┘              │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Workstream 1: Multimodal Message Models (Foundation)

**Goal**: Make the message pipeline support both text and image content.

### 1.1 Domain Layer
```csharp
// New: Domain/ValueObjects/ContentPart.cs
public abstract record ContentPart(string Type);
public sealed record TextContent(string Text) : ContentPart("text");
public sealed record ImageContent(string Url, string? Detail = null) : ContentPart("image_url");

// New: Domain/ValueObjects/MessageContent.cs  
// Wraps either a plain string or a list of ContentParts
public sealed class MessageContent
{
    public string? Text { get; }
    public IReadOnlyList<ContentPart>? Parts { get; }
    public bool IsMultimodal => Parts is not null;
    
    // Factory methods
    public static MessageContent FromText(string text);
    public static MessageContent FromParts(IEnumerable<ContentPart> parts);
    
    // Implicit conversion from string for backward compat
    public static implicit operator MessageContent(string text);
    
    // Serialize to OpenAI format (string or array)
    public object ToOpenAiContent();
}
```

### 1.2 Entity Change
```csharp
// Message entity: Content stays as string in DB (JSON-serialized for multimodal)
// Add computed property for structured access
public class Message : BaseAuditableEntity<Guid>
{
    public string Content { get; private set; }        // raw storage (string or JSON array)
    public MessageContent ParsedContent => MessageContent.Parse(Content);
}
```

### 1.3 Infrastructure Layer (LiteLlm Models)
```csharp
// ChatMessageRequest.Content changes from string to object
public sealed class ChatMessageRequest
{
    public required string Role { get; init; }
    
    [JsonPropertyName("content")]
    public required object Content { get; init; }  // string or List<ContentPartDto>
}

public sealed class ContentPartDto
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    
    [JsonPropertyName("text")]
    public string? Text { get; init; }
    
    [JsonPropertyName("image_url")]
    public ImageUrlDto? ImageUrl { get; init; }
}

public sealed class ImageUrlDto
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }
    
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }  // "low", "high", "auto"
}
```

### 1.4 API Layer
```csharp
// ChatMessage in ChatCompletionRequest: Content becomes object
// SendMessageRequest gains optional Attachments
### 1.4 API Layer — File Upload Pipeline (addresses upload/size constraints)

**Critical Design Decision**: Sending base64 via SignalR will fail —
SendMessageCommandValidator has 32K char max, SignalR has message size limits, and
base64 screenshots can be 500KB+. Need a proper upload pipeline.

**Solution: Separate upload endpoint, reference URLs in messages**
```csharp
// New: POST /api/attachments — multipart form upload
// Returns stable URL for referencing in messages
[HttpPost]
public async Task<AttachmentResponse> Upload(IFormFile file)
{
    // Validate: image types only (png, jpg, webp, gif), max 10MB
    // Store to local filesystem (/data/attachments/{userId}/{guid}.{ext})
    // Return { id, url, contentType, fileName }
}

// SendMessageRequest gains optional attachment references (NOT base64)
public record SendMessageRequest(string Content, string? Model, List<Guid>? AttachmentIds);

// In SendMessage handler: resolve AttachmentIds → URLs → build multimodal content
```

**Storage**: Local filesystem first (`/data/attachments/`), mountable Docker volume.
Migration path to S3/MinIO later via `IAttachmentStorage` interface.

**MCP tool screenshots**: Stored via same pipeline — tool returns base64, handler
saves to storage, gets URL, sends URL in multimodal content to LLM.

### 1.5 Database Migration
- Message.Content column stays as TEXT (no schema change)
- Multimodal content stored as JSON string: `[{"type":"text",...}, {"type":"image_url",...}]`
- Plain text messages stay as plain strings (backward compatible)
- New nullable column: `ContentType` (enum: "text", "multimodal") for efficient filtering
- New table: `attachments` (id, userId, fileName, contentType, storagePath, createdAt)

### 1.6 Frontend
- ChatInput: Add image upload button (paste, drag-drop, file picker)
- Upload via `POST /api/attachments` → get back URL + ID
- Include AttachmentIds in SendMessage request
- Message type: `content: string | ContentPart[]`
- ChatMessage renderer: detect and render image parts inline

**Files Changed**: ~20 files across all layers
**Risk**: Medium — touches core message pipeline, but backward compatible (string still works)

---

## Workstream 2: Tool/Function Calling (LLM ↔ Plugin Bridge)

**Goal**: Enable the LLM to autonomously invoke tools (plugins, MCP tools).

### 2.1 Request Model Extensions
```csharp
// Add to ChatCompletionRequest
public List<ToolDefinition>? Tools { get; init; }
public string? ToolChoice { get; init; }  // "auto", "none", "required", or specific

public sealed class ToolDefinition
{
    public string Type { get; init; } = "function";
    public FunctionDefinition Function { get; init; }
}

public sealed class FunctionDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public JsonElement? Parameters { get; init; }  // JSON Schema
}
```

### 2.2 Response Model Extensions
```csharp
// Add to ChatCompletionResponse/Delta
public List<ToolCall>? ToolCalls { get; init; }

public sealed class ToolCall
{
    public required string Id { get; init; }
    public string Type { get; init; } = "function";
    public FunctionCall Function { get; init; }
}

public sealed class FunctionCall
{
    public required string Name { get; init; }
    public required string Arguments { get; init; }  // JSON string
}
```

### 2.3 Tool Execution Loop in SendMessage Handler

**Critical Design Decision** (per review): Streaming + tool_calls are incompatible in the
current pipeline. Delta models have no tool_calls fields, and SSE→SignalR streaming cannot pause
for tool execution mid-stream.

**Solution: Stepwise Non-Streaming for Tool Loop, Stream Only Final Response**
```
User message → LLM (non-streaming, with tools list) →
  If tool_calls in response:
    Execute each tool → collect results →
    Append tool results as role:"tool" messages →
    Re-call LLM (non-streaming) with updated messages →
    Repeat until LLM returns text (max 5 iterations) →
    Stream ONLY the final text response via SignalR
  Else (no tool_calls):
    Stream text response directly (current behavior)
```

**Edge Cases**:
- Tool call depth limit: max 5 iterations (configurable), abort with error message to user
- Tool execution timeout: 30s per tool call, 120s total for entire loop
- Tool error handling: wrap errors as tool result content, let LLM decide recovery
- Parallel tool calls: execute concurrently when LLM returns multiple tool_calls
- Client progress: send SignalR progress events during tool execution ("Running browser_screenshot...")

### 2.4 Plugin → Tool Adapter
```csharp
// Convert IPlugin to OpenAI tool format
public interface IToolProvider
{
    IReadOnlyList<ToolDefinition> GetToolDefinitions();
    Task<string> ExecuteToolAsync(string name, string arguments, CancellationToken ct);
}

// Adapter for existing IPlugin
public class PluginToolAdapter : IToolProvider { ... }
```

**Files Changed**: ~8 files
**Risk**: Medium — new capability, no existing behavior changes

---

## Workstream 3: MCP Client Integration

**Goal**: Mimir acts as MCP client, connecting to external MCP servers and exposing their tools to the LLM.

### 3.1 MCP Client Service
```csharp
public interface IMcpClientService
{
    Task ConnectAsync(McpServerConfig config, CancellationToken ct);
    Task<IReadOnlyList<ToolDefinition>> GetToolsAsync(CancellationToken ct);
    Task<string> InvokeToolAsync(string name, JsonElement arguments, CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
}
```

### 3.2 MCP Server Configuration & Transport

**Critical Design Decision** (per review): stdio transport requires subprocess spawning
from mimir-api, meaning Node.js must be in the API container. HTTP/SSE transport keeps separation clean.

**Decision: HTTP/SSE Transport — keeps containers decoupled**
- MCP Playwright runs as separate Docker container with SSE endpoint
- mimir-api connects via HTTP/SSE transport (no Node.js in API container)
- Better scaling, clear process boundaries, Docker-native

```yaml
# appsettings.json
"McpServers": [
  {
    "Name": "playwright",
    "Transport": "sse",
    "Url": "http://mimir-mcp-playwright:3000/sse",
    "Enabled": true
  }
]
```

### 3.3 MCP → Tool Bridge
- On startup: connect to configured MCP servers via SSE
- Discover their tools via `tools/list`
- Convert MCP tool schemas to OpenAI `ToolDefinition` format
- When LLM calls a tool: route to correct MCP server via `tools/call`
- Return result to LLM

### 3.4 NuGet Package
- Use `ModelContextProtocol` (.NET MCP SDK by Microsoft) for client implementation
- Supports both stdio and SSE transports out of the box

### 3.5 Docker Compose Addition
```yaml
mimir-mcp-playwright:
  image: node:22-slim
  command: npx @anthropic/mcp-playwright --transport sse --port 3000
  ports:
    - "3000"    # internal only
  networks:
    - mimir-network
  deploy:
    resources:
      limits:
        memory: 1G
        cpus: '1.0'
```

**Files Changed**: ~6 new files + config changes
**Risk**: Low-Medium — additive, no existing behavior changes

---

## Workstream 4: MCP Playwright Server Setup

**Goal**: Deploy MCP Playwright server for browser automation (screenshots, navigation, DOM inspection).

### 4.1 Capabilities Exposed
- `browser_navigate` — Go to URL
- `browser_screenshot` — Capture page/element screenshot (returns base64 PNG)
- `browser_click` — Click element by selector
- `browser_type` — Type text into element
- `browser_get_text` — Extract text content
- `browser_evaluate` — Run JavaScript
- `browser_snapshot` — Accessibility tree snapshot

### 4.2 Integration Flow
```
User: "Check what's on example.com and describe the layout"
→ LLM receives tools list including MCP Playwright tools
→ LLM calls browser_navigate(url="https://example.com")
→ LLM calls browser_screenshot()
→ Screenshot returned as base64 image
→ Image sent back to LLM as multimodal content
→ LLM describes what it sees (requires vision model!)
```

### 4.3 Security
- Playwright runs in Docker container (sandboxed)
- URL allowlist/blocklist configurable
- No access to host network (except via explicit proxy)
- Screenshot size limits (max 2MB)
- Rate limiting on tool calls

**Files Changed**: Docker config + 2-3 new files
**Risk**: Low — isolated service

---

## Workstream 5: Vision-Capable Model Setup

**Goal**: Add a multimodal/vision model to LM Studio + LiteLLM config.

### 5.1 Recommended Models (LM Studio compatible)
| Model | Size | Vision | Quality | Speed |
|-------|------|--------|---------|-------|
| **Qwen2.5-VL-7B-Instruct** | 7B | Yes | Good | Fast |
| Qwen2.5-VL-72B-Instruct | 72B | Yes | Excellent | Slow |
| InternVL2-8B | 8B | Yes | Good | Fast |
| LLaVA-v1.6-34B | 34B | Yes | Very Good | Medium |

**Recommendation**: Start with `Qwen2.5-VL-7B-Instruct` — same family as existing `qwen-2.5-72b`, good vision quality, runs fast on consumer GPU.

### 5.2 LiteLLM Config Addition
```yaml
- model_name: qwen-2.5-vl-7b
  litellm_params:
    model: openai/lm_studio/qwen2.5-vl-7b-instruct
    api_base: http://host.docker.internal:1234/v1
    api_key: lm-studio
```

### 5.3 ConversationSettings Default Update
- Default model stays `qwen-2.5-72b` (text tasks)
- When multimodal content detected, auto-route to vision model
- Or: user can select vision model explicitly per conversation

---

## Implementation Order

```
Phase A (Foundation — no breaking changes):
  W1.1-1.3  Multimodal message models (Domain + Infra)     ~2 days
  W2.1-2.2  Tool calling request/response models            ~1 day
  W5        Vision model setup (LM Studio + LiteLLM)        ~0.5 day

Phase B (Wiring — behavior changes):
  W1.4       Upload pipeline (endpoint + storage + DB)       ~1.5 days
  W1.5       DB migration (ContentType column + attachments) ~0.5 day
  W2.3-2.4  Tool execution loop (non-streaming stepwise)    ~2 days
  W3        MCP client service (SSE transport)               ~2 days

Phase C (Integration):
  W4        MCP Playwright Docker container + config         ~1 day
  W1.6      Frontend multimodal UI (upload + render)         ~2 days
  W3+W4     Wire MCP tools into tool-calling loop            ~1 day

Phase D (Testing & Polish):
  Integration tests for multimodal flow                      ~1 day
  E2E: User uploads image → LLM describes it                ~0.5 day
  E2E: LLM uses Playwright → screenshots → describes        ~0.5 day
  Tool call edge cases (depth limit, timeout, error recovery) ~0.5 day
```

**Total estimated effort**: ~16 working days (updated with upload pipeline + tool edge case testing)

---

## Consequences

### Positive
- LLM can process images/screenshots — enables visual understanding tasks
- MCP Playwright enables autonomous browser automation
- Tool calling unlocks autonomous plugin usage (CodeRunner, WebSearch, MCP tools)
- Architecture extensible to more MCP servers (file system, database, etc.)

### Negative
- Vision models require more GPU memory than text-only
- Multimodal messages increase storage (base64 images in DB)
- Tool calling loop adds latency (multiple LLM roundtrips)
- MCP server adds another Docker container to manage

### Risks
- LM Studio multimodal support varies by model — needs testing
- LiteLLM multimodal passthrough may have edge cases
- Tool calling quality depends heavily on model capability (small models struggle)
- Playwright in Docker needs Chromium, increases image size significantly

### Mitigations
- Start with Qwen2.5-VL-7B (well-tested, good tool following)
- Store images as external files with URL references (not inline base64 in DB) for large deployments
- Implement tool call depth limit (max 5 recursive calls)
- Use headless Chromium with resource limits in Docker

## References
- [OpenAI Vision API](https://platform.openai.com/docs/guides/vision)
- [MCP Specification](https://modelcontextprotocol.io/specification)
- [MCP Playwright](https://github.com/anthropics/mcp-playwright)
- [.NET MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [LiteLLM Multimodal](https://docs.litellm.ai/docs/providers/openai#multimodal)
