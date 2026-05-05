using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Api.Controllers;
using nem.Mimir.Application;
using nem.Mimir.Application.Common.Sanitization;
using nem.Mimir.Infrastructure;
using Shouldly;

namespace nem.Mimir.Api.Tests.Architecture;

public sealed class SanitizationAuditTests
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

    private static readonly string[] SanitizationFieldMarkers = ["content", "message", "prompt"];

    [Fact]
    public void SanitizationAudit_ReportsBodyEndpointsAndSensitiveRequestModels()
    {
        var controllers = typeof(OpenAiCompatController).Assembly
            .GetTypes()
            .Where(IsController)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();

        controllers.ShouldNotBeEmpty();

        var report = controllers
            .Select(AuditController)
            .ToList();

        report.ShouldNotBeEmpty();

        foreach (var entryPoint in LlmEntryPointControllers)
        {
            var controllerStatus = report.FirstOrDefault(x => x.ControllerName == entryPoint)?.Summary
                ?? "missing from current assembly";

            controllerStatus.ShouldNotBeNullOrWhiteSpace();
        }

        report
            .Where(x => x.HasBodyEndpoints)
            .Select(x => $"{x.ControllerName}: {x.Summary}")
            .ToList()
            .ShouldNotBeEmpty();
    }

    [Fact]
    public void SanitizationService_IsRegisteredInInfrastructureDi()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=mimir_tests;Username=postgres;Password=postgres"
            })
            .Build();

        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);

        var descriptor = services.LastOrDefault(service => service.ServiceType == typeof(ISanitizationService));

        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        descriptor.ImplementationType?.Name.ShouldBe("SanitizationService");
    }

    private static ControllerAuditResult AuditController(Type controllerType)
    {
        var bodyEndpoints = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(GetBodyParameters)
            .ToList();

        var sensitiveModels = bodyEndpoints
            .Where(parameter => HasSensitiveField(parameter.ParameterType))
            .Select(parameter => $"{parameter.ParameterType.Name}({string.Join(", ", GetSensitiveFields(parameter.ParameterType))})")
            .ToList();

        var summary = bodyEndpoints.Count == 0
            ? "no [FromBody] endpoints"
            : sensitiveModels.Count == 0
                ? $"{bodyEndpoints.Count} [FromBody] endpoint(s); no message/content/prompt fields detected"
                : $"{bodyEndpoints.Count} [FromBody] endpoint(s); sensitive models: {string.Join("; ", sensitiveModels)}";

        return new ControllerAuditResult(
            controllerType.Name,
            bodyEndpoints.Count > 0,
            summary);
    }

    private static IEnumerable<ParameterInfo> GetBodyParameters(MethodInfo method)
    {
        return method
            .GetParameters()
            .Where(parameter => parameter.GetCustomAttribute<FromBodyAttribute>() is not null);
    }

    private static bool HasSensitiveField(Type modelType)
    {
        return GetSensitiveFields(modelType).Any();
    }

    private static IEnumerable<string> GetSensitiveFields(Type modelType)
    {
        return modelType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Where(propertyName => SanitizationFieldMarkers.Any(marker =>
                propertyName.Contains(marker, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsController(Type type)
    {
        return type is { IsClass: true, IsAbstract: false } &&
               typeof(ControllerBase).IsAssignableFrom(type) &&
               type.Name.EndsWith("Controller", StringComparison.Ordinal);
    }

    private sealed record ControllerAuditResult(string ControllerName, bool HasBodyEndpoints, string Summary);
}
