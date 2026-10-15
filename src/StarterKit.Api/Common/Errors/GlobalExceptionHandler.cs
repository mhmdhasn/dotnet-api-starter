using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace StarterKit.Api.Common.Errors;

/// <summary>Turns unhandled exceptions into RFC 7807 problem responses and hides internals outside Development.</summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string title;
        string? detail;

        switch (exception)
        {
            case AppException appException:
                status = appException.StatusCode;
                title = appException.Title;
                detail = appException.Message;
                logger.LogInformation("Handled application exception {StatusCode}: {Message}", status, exception.Message);
                break;

            case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                // The client went away; nobody is listening for a body.
                httpContext.Response.StatusCode = ClientClosedRequest;
                logger.LogInformation("Request was cancelled by the client.");
                return true;

            case DbUpdateConcurrencyException:
                status = StatusCodes.Status409Conflict;
                title = "Conflict";
                detail = "The resource was modified by another request. Reload it and try again.";
                logger.LogWarning(exception, "Concurrency conflict.");
                break;

            default:
                status = StatusCodes.Status500InternalServerError;
                title = "An unexpected error occurred";
                detail = environment.IsDevelopment() ? exception.ToString() : null;
                logger.LogError(exception, "Unhandled exception.");
                break;
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            }
        });
    }
}
