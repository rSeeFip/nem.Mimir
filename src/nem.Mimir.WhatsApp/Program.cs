using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.WhatsApp.Configuration;
using nem.Mimir.WhatsApp.Health;
using nem.Mimir.WhatsApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<WhatsAppSettings>(
    builder.Configuration.GetSection(WhatsAppSettings.SectionName));

builder.Services.AddSingleton<WhatsAppChannelAdapter>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WhatsAppChannelAdapter>());
builder.Services.AddSingleton<nem.Mimir.Application.ChannelEvents.IChannelMessageSender>(
    sp => sp.GetRequiredService<WhatsAppChannelAdapter>());

builder.Services.AddSingleton<IWhatsAppMediaDownloader, WhatsAppMediaDownloader>();

var whatsAppSettings = builder.Configuration.GetSection(WhatsAppSettings.SectionName);
var accessToken = whatsAppSettings.GetValue<string>("AccessToken") ?? string.Empty;

builder.Services.AddHttpClient(WhatsAppChannelAdapter.HttpClientName, client =>
{
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddStandardResilienceHandler();

builder.Services.AddHttpClient(WhatsAppMediaDownloader.HttpClientName, client =>
{
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    client.Timeout = TimeSpan.FromSeconds(60);
}).AddStandardResilienceHandler();

builder.Services.AddHealthChecks()
    .AddCheck<WhatsAppHealthCheck>("whatsapp", tags: ["ready"]);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(nem.Mimir.Application.ChannelEvents.IngestChannelEventCommand).Assembly));

var app = builder.Build();

app.MapWhatsAppWebhook();

app.MapHealthChecks("/health/whatsapp", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

await app.RunAsync();
