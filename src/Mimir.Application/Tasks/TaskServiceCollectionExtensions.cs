namespace Mimir.Application.Tasks;

using Microsoft.Extensions.DependencyInjection;
using nem.Contracts.Agents;

public static class TaskServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundTaskExecution(this IServiceCollection services)
    {
        services.AddSingleton<BackgroundTaskExecutor>();
        services.AddSingleton<IBackgroundTaskExecutor>(sp => sp.GetRequiredService<BackgroundTaskExecutor>());

        return services;
    }
}
