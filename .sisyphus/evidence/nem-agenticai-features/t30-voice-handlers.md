# Task 30: Voice Pipeline Wolverine Handlers — Evidence

## Status: ✅ COMPLETE

## Summary

Implemented three Wolverine message handlers that orchestrate the voice pipeline in Mimir.Api:

1. **VoiceMessageReceivedHandler** — STT transcription with 60s timeout, OTel span `voice.stt`
2. **VoiceSynthesisRequestHandler** — TTS synthesis with 30s timeout, OTel span `voice.tts`
3. **VoicePipelineOrchestrator** — STT→cognitive→TTS coordinator with per-stage timeouts, OTel span `voice.pipeline`

Plus 6 Wolverine message types and 19 unit tests (exceeding the 15 minimum).

## Files Created

| File | Purpose |
|------|---------|
| `src/Mimir.Api/Handlers/Voice/Messages/VoiceMessages.cs` | 6 sealed record message types |
| `src/Mimir.Api/Handlers/Voice/VoiceMessageReceivedHandler.cs` | STT handler (126 lines) |
| `src/Mimir.Api/Handlers/Voice/VoiceSynthesisRequestHandler.cs` | TTS handler (135 lines) |
| `src/Mimir.Api/Handlers/Voice/VoicePipelineOrchestrator.cs` | Pipeline orchestrator (201 lines) |
| `tests/Mimir.Api.Tests/Handlers/Voice/VoiceHandlerTests.cs` | 19 unit tests across 3 test classes |

## Files Modified

None. All changes are additive (new files only).

## Message Types

```csharp
// Inbound
VoiceMessageReceived(byte[] AudioData, string ChannelId, string UserId, string? LanguageHint, string? MimeType, double? DurationSeconds)
VoiceSynthesisRequest(string Text, string ChannelId, string UserId, string? VoiceId)
VoicePipelineRequest(byte[] AudioData, string ChannelId, string UserId, string? LanguageHint, string? VoiceId)

// Outbound
VoiceMessageResult(string? TranscribedText, double Confidence, string? Language, TimeSpan Duration, bool Success, string? ErrorMessage)
VoiceSynthesisResult(byte[]? AudioData, string MimeType, double? DurationSeconds, bool Success, string? ErrorMessage)
VoicePipelineResult(string? TranscribedText, string? ResponseText, byte[]? ResponseAudio, bool Success, string? ErrorMessage)
```

## Handler Signatures (Wolverine Convention)

All handlers follow the Wolverine static method convention:

```csharp
// VoiceMessageReceivedHandler
public static async Task<VoiceMessageResult> Handle(
    VoiceMessageReceived message, ISpeechToTextProvider sttProvider,
    ILogger<VoiceMessageReceivedHandler> logger, CancellationToken ct)

// VoiceSynthesisRequestHandler
public static async Task<VoiceSynthesisResult> Handle(
    VoiceSynthesisRequest request, ITextToSpeechProvider ttsProvider,
    ILogger<VoiceSynthesisRequestHandler> logger, CancellationToken ct)

// VoicePipelineOrchestrator
public static async Task<VoicePipelineResult> Handle(
    VoicePipelineRequest request, ISpeechToTextProvider sttProvider,
    ITextToSpeechProvider ttsProvider, ILogger<VoicePipelineOrchestrator> logger,
    CancellationToken ct)
```

## OTel Spans

| Handler | Span Name | Tags |
|---------|-----------|------|
| VoiceMessageReceivedHandler | `voice.stt` | audio_length_bytes, language_hint, channel_id, user_id, success, confidence, transcription_length, detected_language, duration_seconds, error |
| VoiceSynthesisRequestHandler | `voice.tts` | text_length, channel_id, user_id, voice_id, success, audio_length_bytes, duration_seconds, error |
| VoicePipelineOrchestrator | `voice.pipeline` | audio_length_bytes, channel_id, user_id, pipeline_stage, success, transcribed_text_length, response_text_length, audio_response_length_bytes, error |

## Test Results

```
xUnit.net v3 In-Process Runner v3.2.2+728c1dce01 (64-bit .NET 10.0.3)
  Discovering: Mimir.Api.Tests
  Discovered:  Mimir.Api.Tests
  Starting:    Mimir.Api.Tests
  Finished:    Mimir.Api.Tests
=== TEST EXECUTION SUMMARY ===
   Mimir.Api.Tests  Total: 154, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 60.287s
```

- **Baseline tests**: 135 (all pass)
- **New voice tests**: 19 (all pass)
- **Total**: 154 (0 failures, 0 errors)

### Test Breakdown

**VoiceMessageReceivedHandlerTests** (6 tests):
- `Handle_ValidAudio_ReturnsSuccessWithTranscription`
- `Handle_EmptyAudio_ReturnsFailureResult`
- `Handle_SttProviderThrows_ReturnsErrorResult`
- `Handle_SttProviderTimesOut_ReturnsTimeoutResult`
- `Handle_SetsOtelTagsOnSuccess`
- `Handle_PreservesLanguageHint`

**VoiceSynthesisRequestHandlerTests** (6 tests):
- `Handle_ValidText_ReturnsSuccessWithAudio`
- `Handle_MultipleChunks_CombinesIntoSingleArray`
- `Handle_TtsProviderThrows_ReturnsErrorResult`
- `Handle_TtsProviderTimesOut_ReturnsTimeoutResult`
- `Handle_SetsCorrectMimeType`
- `Handle_EstimatesDuration`

**VoicePipelineOrchestratorTests** (7 tests):
- `Handle_FullPipeline_ReturnsSuccessResult`
- `Handle_SttFails_ReturnsErrorWithSttStage`
- `Handle_TtsFails_ReturnsTranscribedTextAndError`
- `Handle_SttTimesOut_ReturnsTimeoutError`
- `Handle_TtsTimesOut_PreservesTranscribedText`
- `Handle_V1CognitiveStep_EchoesTranscribedText`
- `TimeoutConstants_AreCorrect`

## Build Verification

```
dotnet build nem.Mimir.sln -c Release
→ Build succeeded. 0 Warning(s). 0 Error(s).
```

## DI Registration

No explicit DI registration needed — Wolverine auto-discovers handlers in the hosting assembly (`Mimir.Api`) via `builder.UseWolverine(...)` in `Program.cs`.

## Design Decisions

1. **Echo-back for V1 cognitive step**: VoicePipelineOrchestrator's cognitive processing step is a simple echo (response = transcribed text). This is explicitly noted for V1 and can be replaced with cognitive message bus dispatch in a future iteration.

2. **Per-stage timeouts**: Each stage (STT: 60s, TTS: 30s) uses `CancellationTokenSource.CreateLinkedTokenSource(ct)` to respect both the handler-specific timeout and the caller's cancellation token.

3. **Graceful error handling**: All handlers catch exceptions and return error results (Success=false, ErrorMessage set) rather than throwing. This follows the Wolverine convention for message handlers where failure should be communicated via the result type.

4. **Audio chunk combination**: Both `VoiceSynthesisRequestHandler` and `VoicePipelineOrchestrator` collect `IAsyncEnumerable<byte[]>` chunks and combine them using `Buffer.BlockCopy` for efficiency.

5. **Duration estimation**: TTS handler estimates audio duration assuming 16kHz mono 16-bit PCM (32000 bytes/second), accounting for 44-byte WAV header.
