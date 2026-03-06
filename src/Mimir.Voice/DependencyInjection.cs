namespace Mimir.Voice;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mimir.Voice.Configuration;
using nem.Contracts.Voice;

/// <summary>
/// Dependency injection extensions for voice services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds voice services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVoiceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // STT services
        services.Configure<WhisperOptions>(
            configuration.GetSection(WhisperOptions.SectionName));

        services.AddSingleton<AudioPreprocessor>();
        services.AddSingleton<IWhisperProcessorFactory, WhisperNetProcessorFactory>();
        services.AddSingleton<ISpeechToTextProvider, WhisperSpeechToTextProvider>();

        // TTS services
        services.Configure<TtsOptions>(
            configuration.GetSection(TtsOptions.SectionName));

        services.AddSingleton<ITtsBackend, PiperTtsBackend>();
        services.AddSingleton<ITextToSpeechProvider, DefaultTextToSpeechProvider>();

        return services;
    }
}
