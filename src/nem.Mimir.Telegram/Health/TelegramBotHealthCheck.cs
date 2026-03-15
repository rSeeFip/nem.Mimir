using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Telegram.Configuration;
using Telegram.Bot;

namespace nem.Mimir.Telegram.Health;

/// <summary>
/// Health check that verifies the Telegram bot can connect to the API.
/// </summary>
internal sealed class TelegramBotHealthCheck : IHealthCheck
{
    private readonly TelegramSettings _settings;
    private readonly ILogger<TelegramBotHealthCheck> _logger;

    public TelegramBotHealthCheck(
        IOptions<TelegramSettings> settings,
        ILogger<TelegramBotHealthCheck> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.BotToken))
            {
                return HealthCheckResult.Unhealthy("Bot token is not configured.");
            }

            var bot = new TelegramBotClient(_settings.BotToken);
            var me = await bot.GetMe(cancellationToken);

            return me is not null
                ? HealthCheckResult.Healthy($"Bot @{me.Username} is connected.")
                : HealthCheckResult.Degraded("Bot connected but could not retrieve info.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Telegram bot health check failed");
            return HealthCheckResult.Unhealthy("Failed to connect to Telegram API.", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Telegram bot health check timed out");
            return HealthCheckResult.Unhealthy("Telegram API health check timed out.", ex);
        }
    }
}
