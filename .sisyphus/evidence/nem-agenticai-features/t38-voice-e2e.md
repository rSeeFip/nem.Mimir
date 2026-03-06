# T38: Voice Pipeline End-to-End Tests — Evidence

## Status: COMPLETE

## Summary

Created comprehensive E2E test suite for the full voice pipeline: Telegram voice note → OGG→WAV → Whisper STT → agent processing → TTS → WAV→OGG → Telegram voice response. Tests cover timeout handling, format validation, duration limits, and OTel span assertions.

## Files Created

1. `tests/Mimir.Voice.Tests/E2E/VoicePipelineE2ETests.cs` — 29 new E2E tests across 6 test classes

## Files Modified

1. `tests/Mimir.Voice.Tests/Mimir.Voice.Tests.csproj` — Added project references to Mimir.Api and Mimir.Telegram

## Test Classes & Counts

| Class | Tests | Coverage |
|-------|-------|----------|
| `VoicePipelineE2ETests` | 5 | Full pipeline: OGG→WAV→STT→TTS→OGG, language hints, multi-chunk TTS, STT failure, TTS failure |
| `VoicePipelineTimeoutTests` | 6 | STT OCE propagation, TTS OCE propagation, handler cancellation, constants verification, external cancel |
| `VoicePipelineFormatValidationTests` | 7 | Valid WAV, 44100Hz reject, 8kHz reject, stereo reject, non-PCM reject, too-short reject, non-RIFF reject |
| `VoicePipelineDurationLimitTests` | 5 | >60s reject, =60s accept, 59s accept, 0.5s accept, preprocessing gate |
| `VoicePipelineOTelTests` | 4 | Pipeline span, STT span, TTS span, error status on timeout |
| `VoiceHandlerE2ETests` | 2 | STT→TTS chained flow, empty audio rejection |

## Test Results

```
Mimir.Voice.Tests  Total: 81, Errors: 0, Failed: 0, Skipped: 0, Time: 1.3s
Mimir.Telegram.Tests  Total: 171, Errors: 0, Failed: 0, Skipped: 0, Time: 3.7s
Mimir.Api.Tests  Total: 154, Errors: 0, Failed: 0, Skipped: 0, Time: 60.3s
```

New E2E tests: 29 (81 total Voice - 52 existing)
All existing tests unaffected.

## Build

```
Build succeeded. 0 Warning(s) 0 Error(s)
```

## Key Design Decisions

1. **Mocked IAudioFormatConverter** — Real AudioFormatConverter requires FFmpeg; mocked at interface boundary
2. **Real AudioPreprocessor** — Used real implementation since it's pure WAV header parsing with no external deps
3. **Fast timeout tests** — Timeout path tests throw OperationCanceledException immediately instead of waiting real 60s/30s (those are already covered in VoiceHandlerTests). Handler cancellation tests use short CTS (50ms)
4. **OTel TagObjects** — Activity.SetTag(string, bool) stores in TagObjects, not Tags (string-only). Tests use TagObjects for bool assertions
5. **ToAsyncEnumerable<byte[]>** — Explicit type parameter required to prevent params T[] from flattening byte[] to individual bytes
