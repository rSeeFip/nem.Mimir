using MediatR;
using Microsoft.Extensions.Logging;

namespace Mimir.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behavior that catches and logs unhandled exceptions from downstream handlers.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public sealed class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> _logger;

    public UnhandledExceptionBehavior(ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex) // Intentional catch-all: MediatR pipeline behavior must log any unhandled exception before re-throwing
        {
            var requestName = typeof(TRequest).Name;

            _logger.LogError(ex, "Unhandled exception for request {RequestName}", requestName);

            throw;
        }
    }
}
