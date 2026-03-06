using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mimir.Signal.Configuration;
using Mimir.Signal.Health;
using Mimir.Signal.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SignalSettings>(
    builder.Configuration.GetSection(SignalSettings.SectionName));

var signalApiBaseUrl = builder.Configuration.GetSection(SignalSettings.SectionName)
    .GetValue<string>("ApiBaseUrl") ?? "http://localhost:8080";

builder.Services.AddHttpClient<SignalApiClient>(client =>
{
    client.BaseAddress = new Uri(signalApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler();

builder.Services.AddHealthChecks()
    .AddCheck<SignalHealthCheck>("signal", tags: ["ready"]);

builder.Services.AddHostedService<SignalChannelAdapter>();

var host = builder.Build();
await host.RunAsync();
