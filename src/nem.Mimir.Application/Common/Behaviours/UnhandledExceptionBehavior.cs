using Microsoft.Extensions.Logging;
using Wolverine;

namespace nem.Mimir.Application.Common.Behaviours;

/// <summary>
/// Wolverine middleware that catches and logs unhandled exceptions from downstream handlers.
/// Applied globally via <c>opts.Policies.AddMiddleware&lt;UnhandledExceptionMiddleware&gt;()</c>.
/// </summary>
/// <remarks>
/// Uses the <c>Finally</c> convention to execute in a finally block after handler completion.
/// Only logs when an exception is present; does not suppress the exception.
/// </remarks>
public static class UnhandledExceptionMiddleware
{
    public static void Finally(Exception? ex, ILogger logger, Envelope envelope)
    {
        if (ex is not null)
        {
            logger.LogError(ex, "Unhandled exception for request {RequestName}", envelope.MessageType);
        }
    }
}
