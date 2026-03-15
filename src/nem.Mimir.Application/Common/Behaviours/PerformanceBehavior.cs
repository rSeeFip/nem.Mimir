using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace nem.Mimir.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behavior that warns when request handling exceeds a performance threshold.
/// Logs a warning for requests that take longer than 500 milliseconds.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int WarningThresholdMilliseconds = 500;

    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly Stopwatch _timer;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
        _timer = new Stopwatch();
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _timer.Start();

        var response = await next();

        _timer.Stop();

        var elapsedMilliseconds = _timer.ElapsedMilliseconds;

        if (elapsedMilliseconds > WarningThresholdMilliseconds)
        {
            var requestName = typeof(TRequest).Name;

            _logger.LogWarning(
                "Long running request: {RequestName} ({ElapsedMilliseconds}ms)",
                requestName,
                elapsedMilliseconds);
        }

        return response;
    }
}
