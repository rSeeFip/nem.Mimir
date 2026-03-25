using Microsoft.Extensions.Logging;
using Wolverine;

namespace nem.Mimir.Application.Common.Behaviours;

/// <summary>
/// Wolverine middleware that logs request and response information using structured logging.
/// Applied globally via <c>opts.Policies.AddMiddleware&lt;LoggingMiddleware&gt;()</c>.
/// </summary>
public sealed class LoggingMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public void Before(Envelope envelope)
    {
        _logger.LogInformation("Handling {RequestName}", envelope.MessageType);
    }

    public void After(Envelope envelope)
    {
        _logger.LogInformation("Handled {RequestName}", envelope.MessageType);
    }
}
