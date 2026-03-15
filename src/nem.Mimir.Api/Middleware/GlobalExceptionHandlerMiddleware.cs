using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common;
using nem.Mimir.Application.Common.Exceptions;

namespace nem.Mimir.Api.Middleware;

/// <summary>
/// Global exception handler middleware that catches unhandled exceptions
/// and returns appropriate ProblemDetails JSON responses.
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — not an error, don't log as warning/error
            _logger.LogDebug("Request was cancelled by the client: {Path}", context.Request.Path);
            context.Response.StatusCode = 499; // Client Closed Request (nginx convention)
        }
        catch (Exception ex) // Intentional catch-all: global exception middleware must handle all unhandled exceptions from the request pipeline
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
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
            _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception occurred: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);
        }

        // Never expose internal exception messages to clients — use generic messages
        // for all error types to prevent information leakage
        var detail = exception switch
        {
            ValidationException => "One or more validation errors occurred.",
            NotFoundException notFound => notFound.Message, // NotFoundException messages are user-facing by design
            ForbiddenAccessException => "You do not have permission to perform this action.",
            ConflictException => "The request conflicts with the current state of the resource.",
            _ => "An unexpected error occurred. Please try again later."
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        // Add trace ID for correlation
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        // Add validation errors if applicable
        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problemDetails, JsonDefaults.Options, context.RequestAborted);
    }
}
