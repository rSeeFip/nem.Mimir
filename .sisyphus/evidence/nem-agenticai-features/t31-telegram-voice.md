# T31: Telegram Voice Integration — Evidence

## Status: COMPLETE

## Summary

Added voice note extraction and OGG<->WAV audio format conversion to Mimir.Telegram.
Wired the Telegram adapter to detect voice messages and route them through the voice pipeline.

## Files Created

| File | Purpose |
|------|---------|
| `src/Mimir.Telegram/Voice/AudioFormatConverter.cs` | `IAudioFormatConverter` (public) + `AudioFormatConverter` (internal). OGG<->WAV conversion with 44-byte WAV header generation, 16kHz/mono/PCM16 target, 60s max duration. V1 passthrough (Concentus swap-in ready). |
| `src/Mimir.Telegram/Voice/TelegramVoiceExtractor.cs` | `ITelegramVoiceExtractor` (public) + `TelegramVoiceExtractor` (internal). Downloads voice file via `GetFile`/`DownloadFile`, converts OGG->WAV via converter. |
| `src/Mimir.Telegram/Voice/VoiceMessageHandler.cs` | `IVoiceMessageHandler` (public) + `VoiceMessageHandler` (internal). Handles incoming voice messages (extract + status messages) and sends voice responses (WAV->OGG + `SendVoice`). |
| `tests/Mimir.Telegram.Tests/Voice/TelegramVoiceExtractorTests.cs` | 32 tests across 3 test classes: `AudioFormatConverterTests` (14), `TelegramVoiceExtractorTests` (7), `VoiceMessageHandlerTests` (11). |

## Files Modified

| File | Change |
|------|--------|
| `src/Mimir.Telegram/Services/TelegramBotService.cs` | Added `IVoiceMessageHandler?` optional dependency. Modified `ProcessUpdateAsync` to detect `Voice` messages before text guard — routes to voice handler. |
| `src/Mimir.Telegram/Program.cs` | Registered `IAudioFormatConverter`, `ITelegramVoiceExtractor`, `IVoiceMessageHandler` in DI as singletons. |

## Test Results

```
xUnit.net v3 — Mimir.Telegram.Tests
Total: 171, Errors: 0, Failed: 0, Skipped: 0
(139 existing + 32 new voice tests)
```

## Build Results

```
dotnet build nem.Mimir.sln -c Release
Build succeeded. 0 Warning(s) 0 Error(s)
```

## Design Decisions

1. **Interfaces made `public`, implementations `internal`** — Matches existing `IMessageHandler`/`ICommandHandler` pattern. Resolves NSubstitute `TypeLoadException` (Castle.DynamicProxy cannot implement internal interfaces even with `InternalsVisibleTo` + PublicKey).
2. **`IVoiceMessageHandler?` as optional parameter** — Avoids breaking existing 4-param constructor calls in tests. DI resolves it when registered.
3. **Voice check before text guard** — `ProcessUpdateAsync` checks `update.Message.Voice` before `update.Message.Text`, preventing silent drop of voice messages.
4. **Separate `ActivitySource("nem.Mimir.Telegram.Voice")`** — Mimir.Telegram has no reference to `NemActivitySource` in Mimir.Application.
5. **V1 passthrough codec** — `DecodeOggToPcm`/`EncodePcmToOgg` pass bytes through. Interface abstraction allows swapping in Concentus later.
6. **`using TelegramVoice = Telegram.Bot.Types.Voice`** — Required alias to avoid collision with `Mimir.Telegram.Voice` namespace.

## OTel Spans

- `voice.convert.ogg_to_wav` — Tags: input_size_bytes, output_size_bytes, duration_seconds
- `voice.convert.wav_to_ogg` — Tags: input_size_bytes, output_size_bytes
- `voice.extract` — Tags: file_id, duration_seconds, mime_type, downloaded_size_bytes, wav_size_bytes, success
- `voice.handle_incoming` — Tags: chat_id, user_id, duration_seconds, wav_size_bytes, success, error
- `voice.send_response` — Tags: chat_id, wav_size_bytes, ogg_size_bytes, success, error
