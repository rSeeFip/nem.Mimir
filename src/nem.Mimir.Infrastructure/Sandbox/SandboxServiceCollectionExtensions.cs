namespace nem.Mimir.Infrastructure.Sandbox;

using Microsoft.Extensions.DependencyInjection;
using nem.Contracts.Sandbox;

public static class SandboxServiceCollectionExtensions
{
    public static IServiceCollection AddOpenSandboxProvider(
        this IServiceCollection services,
        OpenSandboxOptions config)
    {
        ArgumentNullException.ThrowIfNull(config);

        services.Configure<OpenSandboxOptions>(options =>
        {
            options.BaseUrl = config.BaseUrl;
            options.ApiKey = config.ApiKey;
            options.DefaultImage = config.DefaultImage;
            options.TimeoutSeconds = config.TimeoutSeconds;
        });

        services.AddHttpClient(OpenSandboxProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(config.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
            }
        });

        services.AddSingleton<ISandboxProvider, OpenSandboxProvider>();
        services.AddSingleton<OpenSandboxPoolManager>();

        return services;
    }
}
