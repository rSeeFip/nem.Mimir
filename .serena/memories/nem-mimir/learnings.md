# Mimir Project Learnings & Patterns

## T18: Models API Controller Implementation

### Architecture Patterns
- **Clean Architecture Layers**: Domain → Application → Infrastructure → Api
- **File-scoped namespaces**: All files use `namespace Mimir.X.Y;`
- **Sealed classes**: Controllers and internal implementation classes use `public sealed class` or `internal sealed class`
- **Primary constructors**: All classes inject dependencies via primary constructor parameters

### Controller Pattern (from ConversationsController/AdminController)
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]  // or [Authorize(Policy = "RequireAdmin")]
[Produces("application/json")]
public sealed class XyzController : ControllerBase
{
    // Primary constructor
    public XyzController(IService service, ILogger<XyzController> logger)
    {
        _service = service;
        _logger = logger;
    }
    
    // Every method:
    // - Has full XML docs (summary, param, returns)
    // - Has [ProducesResponseType] for each possible response
    // - Has CancellationToken as last parameter
    // - Returns Ok()/Created()/NoContent()/NotFound()
}
```

### LLM Service Pattern (from LiteLlmClient)
- HttpClient obtained via `IHttpClientFactory.CreateClient(HttpClientName)`
- HttpClientName registered in Infrastructure setup
- Application-layer DTOs returned (LlmModelInfoDto, LlmResponse, etc.)
- Error handling: try-catch for network errors, fallback to safe defaults
- Nullability: Use `.Cast<string>()` when Where() filters nulls but type system needs assurance

### Model Metadata Mapping
Known models hardcoded in controller/service:
- `phi-4-mini`: 16384 tokens, "Fast responses, simple queries"
- `qwen-2.5-72b`: 131072 tokens, "Complex reasoning, detailed analysis"  
- `qwen-2.5-coder-32b`: 131072 tokens, "Code generation, technical tasks"
- `nomic-embed-text`: 8192 tokens, "Text embeddings (not for chat)"

### Caching with IMemoryCache
```csharp
// Register in Program.cs
builder.Services.AddMemoryCache();

// Use in controller
var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
{
    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
    return await _service.MethodAsync(ct);
});
```

### Integration Test Pattern (from ConversationsControllerTests)
- Use `[Collection(IntegrationTestCollection.Name)]` with MimirWebApplicationFactory
- Test 401 unauthorized without token using Theory + InlineData
- Avoid testing authenticated success paths (JWT validation bypassed in test env)
- Use `ReadFromJsonAsync<T>()` not `ReadAsAsync<T>()`
- Test factory sets LiteLlm:BaseUrl to empty string → /v1/models calls fail gracefully

### Key Gotchas & Solutions
1. **Nullability issues**: When filtering with `.Where(x => !string.IsNullOrWhiteSpace(x))`, use `.Cast<string>()` before ToHashSet
2. **Token validation**: In test environment, set [Authorize] but don't expect token validation to work with dummy tokens
3. **JSON deserialization**: Use `ReadFromJsonAsync<T>()` for response bodies, not the deprecated `ReadAsAsync<T>()`
4. **Cache key naming**: Use consistent string constants to avoid mismatches

### New Files Created
- `src/Mimir.Application/Common/Models/LlmModelInfoDto.cs` - DTO with record type
- `src/Mimir.Api/Controllers/ModelsController.cs` - REST API controller
- `tests/Mimir.Api.IntegrationTests/ModelsControllerTests.cs` - Integration tests

### Modified Files
- `src/Mimir.Application/Common/Interfaces/ILlmService.cs` - Added GetAvailableModelsAsync method
- `src/Mimir.Infrastructure/LiteLlm/LiteLlmClient.cs` - Implemented GetAvailableModelsAsync
- `src/Mimir.Infrastructure/LiteLlm/LiteLlmModels.cs` - Added ModelsListResponse, ModelInfo classes
- `src/Mimir.Api/Program.cs` - Added builder.Services.AddMemoryCache()

### Build & Test Status
- `dotnet build`: 0 errors, 0 warnings ✓
- `dotnet test`: 447 tests total, all passed ✓
  - Domain: 160 tests
  - Application: 115 tests
  - Infrastructure: 127 tests
  - Integration: 45 tests

## [2026-02-28] Task: P4 - OpenAI-Compatible SSE Endpoints

### OpenAI API Compatibility Pattern
- Route MUST be `[Route("v1")]` (not `api/v1`) — openchat-ui expects `/v1/chat/completions` and `/v1/models`
- All JSON properties use snake_case via `[JsonPropertyName("finish_reason")]` attributes
- SSE format: `data: {json}\n\n` per chunk, `data: [DONE]\n\n` terminator
- Response IDs follow `chatcmpl-{guid}` format
- `created` field is Unix timestamp in seconds
- Non-streaming returns `chat.completion` object; streaming returns `chat.completion.chunk` objects
- First streaming chunk includes `delta.role = "assistant"`; subsequent chunks have `role = null`

### SSE Streaming in ASP.NET Core
- Set `Response.ContentType = "text/event-stream"`, `Cache-Control: no-cache`, `Connection: keep-alive`
- Call `Response.StartAsync()` to force headers to be sent immediately before streaming
- Use `Response.WriteAsync($"data: {json}\n\n")` + `Response.Body.FlushAsync()` for each chunk
- Use `JsonIgnoreCondition.WhenWritingNull` to omit null fields (OpenAI convention)
- Controller action returns `Task` (not `Task<IActionResult>`) — writes directly to Response

### Event Publishing for OpenAI-compat Endpoints
- `ChatRequestReceived` needs a `ConversationId` — generate ephemeral `Guid.NewGuid()` for OpenAI-compat calls
- `ChatCompleted` also needs `MessageId` — generate ephemeral Guid since no persistence
- For streaming, estimate prompt tokens (~4 chars/token) and count chunks as completion tokens

### New Files Created (P4)
- `src/Mimir.Api/Models/OpenAi/ChatCompletionRequest.cs` — Request DTO
- `src/Mimir.Api/Models/OpenAi/ChatCompletionResponse.cs` — Response DTOs (chunk + non-streaming)
- `src/Mimir.Api/Models/OpenAi/OpenAiModelsResponse.cs` — Models list DTO
- `src/Mimir.Api/Controllers/OpenAiCompatController.cs` — Controller with SSE streaming

### Build & Test Status (P4)
- `dotnet build`: 0 errors, 0 warnings ✓
- `dotnet test`: 569 tests total, all passed ✓
  - Domain: 160, Application: 130, Infrastructure: 151, Tui: 44, Telegram: 39, Integration: 45
