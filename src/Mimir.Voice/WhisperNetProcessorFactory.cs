namespace Mimir.Voice;

using Microsoft.Extensions.Options;
using Mimir.Voice.Configuration;
using Whisper.net;

/// <summary>
/// Factory that creates Whisper.net processors wrapping the actual whisper.cpp library.
/// </summary>
internal sealed class WhisperNetProcessorFactory : IWhisperProcessorFactory, IDisposable
{
    private readonly WhisperFactory _factory;

    public WhisperNetProcessorFactory(IOptions<WhisperOptions> options)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.ModelPath))
        {
            throw new InvalidOperationException("Whisper model path is not configured.");
        }

        _factory = WhisperFactory.FromPath(config.ModelPath);
    }

    public IWhisperProcessorWrapper CreateProcessor(string? language)
    {
        var builder = _factory.CreateBuilder();

        if (!string.IsNullOrWhiteSpace(language))
        {
            builder.WithLanguage(language);
        }

        var processor = builder.Build();
        return new WhisperNetProcessorWrapper(processor);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}

/// <summary>
/// Wraps a Whisper.net processor instance.
/// </summary>
internal sealed class WhisperNetProcessorWrapper : IWhisperProcessorWrapper
{
    private readonly WhisperProcessor _processor;

    public WhisperNetProcessorWrapper(WhisperProcessor processor)
    {
        _processor = processor;
    }

    public async IAsyncEnumerable<TranscriptionSegment> ProcessAsync(
        float[] samples,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // Create a MemoryStream from float samples for Whisper.net
        using var stream = new MemoryStream();
        foreach (var sample in samples)
        {
            var bytes = BitConverter.GetBytes(sample);
            await stream.WriteAsync(bytes, ct);
        }
        stream.Position = 0;

        await foreach (var segment in _processor.ProcessAsync(stream, ct))
        {
            yield return new TranscriptionSegment(
                segment.Text,
                segment.Start,
                segment.End);
        }
    }

    public void Dispose()
    {
        _processor.Dispose();
    }
}
