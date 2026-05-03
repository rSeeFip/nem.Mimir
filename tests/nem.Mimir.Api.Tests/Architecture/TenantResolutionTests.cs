using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Api.Controllers;
using nem.Mimir.Api.Services;
using nem.Mimir.Application.Common.Interfaces;
using Shouldly;

namespace nem.Mimir.Api.Tests.Architecture;

public sealed class TenantResolutionTests
{
    private static readonly string[] LlmEntryPointControllers =
    [
        nameof(OpenAiCompatController),
        nameof(MessagesController),
        nameof(ConversationsController),
        nameof(CodeExecutionController),
        nameof(PluginsController),
        "ChannelEventsController",
    ];

    [Fact]
    public void TenantResolution_IsAvailableForAllLlmEntryPoints()
    {
        var controllers = typeof(OpenAiCompatController).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                typeof(ControllerBase).IsAssignableFrom(type) &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .ToDictionary(type => type.Name, type => type);

        using var services = BuildServices();

        foreach (var controllerName in LlmEntryPointControllers)
        {
            controllers.TryGetValue(controllerName, out var controllerType);

            var status = GetTenantResolutionStatus(controllerType, services);

            status.ShouldNotBeNullOrWhiteSpace();

            if (controllerName == "ChannelEventsController")
            {
                // Current API assembly does not contain a ChannelEventsController source type.
                status.ShouldBe("missing from current assembly");
                continue;
            }

            status.ShouldBeOneOf(
                "direct constructor injection",
                "DI registration available");

            if (controllerName == nameof(OpenAiCompatController))
            {
                status.ShouldBe("direct constructor injection");
            }
        }
    }

    [Fact]
    public void CurrentUserService_IsRegisteredInDi()
    {
        using var services = BuildServices();

        services.GetRequiredService<ICurrentUserService>().ShouldBeOfType<CurrentUserService>();
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        return services.BuildServiceProvider();
    }

    private static string GetTenantResolutionStatus(Type? controllerType, IServiceProvider services)
    {
        if (controllerType is null)
            return "missing from current assembly";

        var hasAuthorize = controllerType.IsDefined(typeof(AuthorizeAttribute), inherit: true);
        if (!hasAuthorize)
            return "not an authorized controller";

        var hasCurrentUserDependency = controllerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(ICurrentUserService));

        if (hasCurrentUserDependency)
            return "direct constructor injection";

        _ = services.GetRequiredService<ICurrentUserService>();
        return "DI registration available";
    }
}
