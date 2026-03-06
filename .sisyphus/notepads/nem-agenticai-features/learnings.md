# nem-agenticai-features — Learnings

## Task 30: Voice Pipeline Wolverine Handlers

### Wolverine Handler Convention
- `public sealed class` with `public static async Task<T> Handle(MessageType, ...deps..., CancellationToken)`
- Static methods — dependencies injected by Wolverine's IoC container
- Auto-discovered in the hosting assembly (no explicit registration needed)
- Handler is discovered because `builder.UseWolverine(...)` scans the calling assembly

### Voice Contract Gotchas
- `ITextToSpeechProvider.SynthesizeAsync()` returns `IAsyncEnumerable<byte[]>` (NOT `Task<IAsyncEnumerable<...>>`)
- `ISpeechToTextProvider.TranscribeAsync()` takes `Stream` (not `byte[]`)
- `TranscriptionResult` has `required string Text` — always non-null on success

### NSubstitute + IAsyncEnumerable
- **Critical**: NSubstitute `.Returns()` with `IAsyncEnumerable<byte[]>` requires explicit type parameter
- `ToAsyncEnumerable(new byte[] { 1 })` infers `T = byte` (WRONG) — must use `ToAsyncEnumerable<byte[]>(new byte[] { 1 })`
- Helper pattern `SetupTtsReturns(params byte[][] chunks)` avoids this pitfall since `chunks` is already `byte[][]`

### OTel in Mimir
- `NemActivitySource.StartActivity("span.name")` returns `Activity?` (nullable)
- Tags set with `activity?.SetTag("key", value)` — use null-conditional
- Error status: `activity?.SetStatus(ActivityStatusCode.Error, message)`

### xUnit v3 + .NET 10
- Build as exe, run directly: `./tests/Mimir.Api.Tests/bin/Release/net10.0/Mimir.Api.Tests`
- `TestContext.Current.CancellationToken` for async test cancellation
- `TreatWarningsAsErrors=true` — all warnings must be resolved

### Test Count Tracking
- Baseline: 135 tests
- After T30: 154 tests (+19 voice handler tests)

## Task 31: Telegram Voice Integration

### NSubstitute + Internal Interfaces = TypeLoadException
- `internal interface` + `InternalsVisibleTo("DynamicProxyGenAssembly2", PublicKey=...)` is NOT sufficient in .NET 10 / NSubstitute 5.3.0
- Castle.DynamicProxy fails with `TypeLoadException: Type 'Castle.Proxies.ObjectProxy_X' from assembly 'DynamicProxyGenAssembly2' is attempting to implement an inaccessible interface`
- **Fix**: Make interfaces `public`, keep implementations `internal` — matches existing `IMessageHandler`/`ICommandHandler` pattern

### Telegram.Bot v22.9.0 API Surface
- Extension methods (`SendMessage`, `SendVoice`, `GetFile`) wrap request objects internally
- Mock via `_bot.SendRequest(Arg.Any<GetFileRequest>(), ...)` for NSubstitute
- `Voice` inherits `FileBase` (FileId, FileUniqueId, FileSize) + Duration (int), MimeType (string?)
- `DownloadFile(filePath, stream)` — writes to destination stream, use `When/Do` for NSubstitute
- `InputFile.FromStream(stream, filename)` for `SendVoice`

### Namespace Collision: `Telegram.Bot.Types.Voice` vs `Mimir.Telegram.Voice`
- Must use `using TelegramVoice = Telegram.Bot.Types.Voice;` alias in all Voice namespace files
- Or `global::Telegram.Bot.Types.Voice` in test files

### Optional Constructor Parameters for Backward Compatibility
- Adding `IVoiceMessageHandler? voiceMessageHandler = null` as optional last parameter avoids breaking existing 4-param constructor calls in tests
- DI still resolves the registered service; tests that don't need voice handler get `null` automatically

### TelegramBotService Voice Detection Order
- `ProcessUpdateAsync` MUST check `update.Message.Voice` BEFORE `update.Message.Text` null guard
- Otherwise voice messages (which have `Text == null`) are silently dropped by the text guard

### Test Count Tracking (continued)
- After T31: 171 tests (+32 voice tests: 14 AudioFormatConverter + 7 VoiceExtractor + 11 VoiceMessageHandler)

## Task 38: Voice Pipeline End-to-End Tests

### Activity.SetTag(string, bool) → TagObjects NOT Tags
- `Activity.Tags` returns `IEnumerable<KeyValuePair<string, string?>>` — ONLY string values
- `Activity.SetTag("key", false)` stores in `TagObjects` (object values), not `Tags`
- Use `activity.TagObjects.FirstOrDefault(t => t.Key == "voice.success")` for bool tags
- `Activity.Tags` returns a `default` KeyValuePair with null value if key not found

### IAsyncEnumerable + NSubstitute: params T[] Flattening
- `ToAsyncEnumerable(params T[] items)` with `new byte[] { 1, 2, 3 }` infers `T = byte` (3 items)
- MUST use `ToAsyncEnumerable<byte[]>(new byte[] { 1, 2, 3 })` for single byte[] chunk
- Multi-chunk works naturally: `ToAsyncEnumerable(chunk1, chunk2, chunk3)` since each arg is byte[]

### Timeout Test Strategy for Hardcoded Timeouts
- VoicePipelineOrchestrator catch: `when (!ct.IsCancellationRequested)` — throwing OCE with CancellationToken.None as external ct hits timeout branch immediately
- VoiceMessageReceivedHandler catch: `when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)` — can't simulate without waiting real 60s because timeoutCts is internal
- **Strategy**: Use ThrowsAsync<OperationCanceledException> for orchestrator (fast). For handlers, test external cancellation propagation with short CTS (50ms) + Should.ThrowAsync<TaskCanceledException>
- Real timeout tests (waiting 60s) are in VoiceHandlerTests — don't duplicate in E2E

### Test Count Tracking (continued)
- After T38: 81 Voice tests (+29 E2E), 171 Telegram, 154 Api — all passing
