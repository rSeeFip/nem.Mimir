using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.Voice.Configuration;

namespace Mimir.Voice;

/// <summary>
/// TTS backend implementation that wraps the piper-tts command-line binary.
/// Invokes piper via Process.Start and captures WAV output from stdout.
/// </summary>
internal sealed class PiperTtsBackend : ITtsBackend
{
    private readonly ILogger<PiperTtsBackend> _logger;
    private readonly TtsOptions _options;

    public PiperTtsBackend(
        ILogger<PiperTtsBackend> logger,
        IOptions<TtsOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(string text, string voiceModel, CancellationToken ct)
    {
        var modelPath = string.IsNullOrWhiteSpace(voiceModel)
            ? _options.ModelPath
            : voiceModel;

        _logger.LogDebug("Starting piper-tts synthesis with model {ModelPath}", modelPath);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "piper",
                Arguments = $"--model \"{modelPath}\" --output-raw",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Write input text and close stdin
        await process.StandardInput.WriteAsync(text.AsMemory(), ct);
        process.StandardInput.Close();

        // Read raw PCM output from stdout
        using var outputStream = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(outputStream, ct);

        // Read any error output
        var errorOutput = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            _logger.LogError("piper-tts exited with code {ExitCode}: {ErrorOutput}", process.ExitCode, errorOutput);
            throw new InvalidOperationException(
                $"piper-tts process failed with exit code {process.ExitCode}: {errorOutput}");
        }

        _logger.LogDebug("piper-tts synthesis completed, {ByteCount} bytes of raw PCM produced", outputStream.Length);

        return outputStream.ToArray();
    }
}
