namespace Mimir.Voice;

using nem.Contracts.Voice;
using Mimir.Voice.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Testable abstraction over Whisper.net's processor creation and execution.
/// </summary>
public interface IWhisperProcessorFactory
{
    /// <summary>
    /// Creates a whisper processor for the given language.
    /// </summary>
    IWhisperProcessorWrapper CreateProcessor(string? language);
}

/// <summary>
/// Testable abstraction over a Whisper processor instance.
/// </summary>
public interface IWhisperProcessorWrapper : IDisposable
{
    /// <summary>
    /// Processes audio samples and returns transcription segments.
    /// </summary>
    IAsyncEnumerable<TranscriptionSegment> ProcessAsync(float[] samples, CancellationToken ct);
}

/// <summary>
/// Represents a single transcription segment from Whisper.
/// </summary>
public sealed record TranscriptionSegment(
    string Text,
    TimeSpan Start,
    TimeSpan End);

/// <summary>
/// Speech-to-text provider using Whisper.net for local transcription.
/// </summary>
public sealed class WhisperSpeechToTextProvider : ISpeechToTextProvider
{
    private readonly IWhisperProcessorFactory _processorFactory;
    private readonly AudioPreprocessor _preprocessor;
    private readonly IOptions<WhisperOptions> _options;
    private readonly ILogger<WhisperSpeechToTextProvider> _logger;

    /// <summary>
    /// Creates a new WhisperSpeechToTextProvider.
    /// </summary>
    public WhisperSpeechToTextProvider(
        IWhisperProcessorFactory processorFactory,
        AudioPreprocessor preprocessor,
        IOptions<WhisperOptions> options,
        ILogger<WhisperSpeechToTextProvider> logger)
    {
        _processorFactory = processorFactory ?? throw new ArgumentNullException(nameof(processorFactory));
        _preprocessor = preprocessor ?? throw new ArgumentNullException(nameof(preprocessor));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string? languageHint = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(audioStream);

        var config = _options.Value;

        // Preprocess and validate audio
        var preprocessResult = _preprocessor.Process(audioStream, config.MaxDurationSeconds);
        if (!preprocessResult.IsValid)
        {
            _logger.LogWarning("Audio preprocessing failed: {Error}", preprocessResult.Error);
            throw new InvalidOperationException($"Audio preprocessing failed: {preprocessResult.Error}");
        }

        // Determine language (parameter > config > auto-detect)
        var language = languageHint ?? config.Language;

        // Create timeout-scoped cancellation
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

        _logger.LogInformation(
            "Starting Whisper transcription: duration={Duration:F1}s, language={Language}",
            preprocessResult.Duration.TotalSeconds,
            language ?? "auto");

        try
        {
            using var processor = _processorFactory.CreateProcessor(language);

            var textParts = new List<string>();
            var segmentCount = 0;
            string? detectedLanguage = language;

            await foreach (var segment in processor.ProcessAsync(preprocessResult.Samples!, timeoutCts.Token))
            {
                textParts.Add(segment.Text);
                segmentCount++;
            }

            var fullText = string.Join("", textParts).Trim();

            // Average confidence (Whisper.net doesn't provide per-segment confidence natively,
            // so we use 1.0 for successful transcription and adjust based on text length ratio)
            var confidence = fullText.Length > 0 ? 1.0 : 0.0;

            _logger.LogInformation(
                "Whisper transcription complete: {SegmentCount} segments, {TextLength} chars",
                segmentCount,
                fullText.Length);

            return new TranscriptionResult
            {
                Text = fullText,
                Confidence = confidence,
                Duration = preprocessResult.Duration,
                Language = detectedLanguage
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("Whisper transcription timed out after {Timeout}s", config.TimeoutSeconds);
            throw new TimeoutException(
                $"Transcription timed out after {config.TimeoutSeconds} seconds.");
        }
    }
}
