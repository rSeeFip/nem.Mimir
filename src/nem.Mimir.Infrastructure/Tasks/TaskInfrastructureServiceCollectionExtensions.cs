namespace nem.Mimir.Infrastructure.Tasks;

using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Application.Tasks;

public static class TaskInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundTaskInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BackgroundTaskOptions>(configuration.GetSection(BackgroundTaskOptions.SectionName));

        services.AddSingleton(_ => Channel.CreateUnbounded<BackgroundTaskItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        }));

        services.AddScoped<IBackgroundTaskStateStore, EfCoreBackgroundTaskStateStore>();
        services.AddSingleton<IBackgroundTaskRuntime, BackgroundTaskRuntime>();
        services.AddHostedService<BackgroundTaskProcessor>();

        return services;
    }
}
