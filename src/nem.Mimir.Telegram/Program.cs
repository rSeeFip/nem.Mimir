using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using nem.Mimir.Telegram.Configuration;
using nem.Mimir.Telegram.Handlers;
using nem.Mimir.Telegram.Health;
using nem.Mimir.Telegram.Services;
using nem.Mimir.Telegram.Voice;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<TelegramSettings>(
    builder.Configuration.GetSection(TelegramSettings.SectionName));

// Services
builder.Services.AddSingleton<UserStateManager>();
builder.Services.AddSingleton<AuthenticationService>();
builder.Services.AddSingleton<ICommandHandler, CommandHandler>();
builder.Services.AddSingleton<IMessageHandler, MessageHandler>();

// Voice pipeline
builder.Services.AddSingleton<IAudioFormatConverter, AudioFormatConverter>();
builder.Services.AddSingleton<ITelegramVoiceExtractor, TelegramVoiceExtractor>();
builder.Services.AddSingleton<IVoiceMessageHandler, VoiceMessageHandler>();

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
