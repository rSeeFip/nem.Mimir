using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Mimir.Application.Common;
using Mimir.Application.Common.Exceptions;

namespace Mimir.Api.Middleware;

/// <summary>
/// Global exception handler that returns RFC 7807 ProblemDetails responses.
/// Implements <see cref="IExceptionHandler"/> for use with the built-in
/// <c>UseExceptionHandler()</c> middleware pipeline.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request was cancelled by the client: {Path}", httpContext.Request.Path);
            httpContext.Response.StatusCode = 499; // Client Closed Request (nginx convention)
            return true;
        }

        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation Error"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);
        }
        else
        {
            logger.LogWarning(exception, "Handled exception occurred: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);
        }

        var detail = exception switch
        {
            ValidationException => "One or more validation errors occurred.",
            NotFoundException notFound => notFound.Message,
            ForbiddenAccessException => "You do not have permission to perform this action.",
            ConflictException => "The request conflicts with the current state of the resource.",
            // Never expose internal exception messages to clients — use generic messages
            // for all error types to prevent information leakage
            _ => "An unexpected error occurred. Please try again later."
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Type = statusCode switch
            {
                StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            }
        };

        // Add trace ID for correlation
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        // Add validation errors if applicable
        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, JsonDefaults.Options, cancellationToken);

        return true;
    }
}
