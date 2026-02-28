using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mimir.Telegram.Configuration;
using Mimir.Telegram.Handlers;
using Mimir.Telegram.Health;
using Mimir.Telegram.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<TelegramSettings>(
    builder.Configuration.GetSection(TelegramSettings.SectionName));

// Services
builder.Services.AddSingleton<UserStateManager>();
builder.Services.AddSingleton<AuthenticationService>();
builder.Services.AddSingleton<CommandHandler>();
builder.Services.AddSingleton<MessageHandler>();

// HTTP clients
var apiBaseUrl = builder.Configuration.GetSection(TelegramSettings.SectionName)
    .GetValue<string>("ApiBaseUrl") ?? "http://localhost:5000";

builder.Services.AddHttpClient<MimirApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(120);
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient("keycloak", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<TelegramBotHealthCheck>("telegram-bot", tags: ["ready"]);

// Hosted service
builder.Services.AddHostedService<TelegramBotService>();

var host = builder.Build();
await host.RunAsync();
