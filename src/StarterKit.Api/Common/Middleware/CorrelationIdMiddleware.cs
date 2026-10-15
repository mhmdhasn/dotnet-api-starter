using System.Text.RegularExpressions;
using Serilog.Context;

namespace StarterKit.Api.Common.Middleware;

/// <summary>
/// Reads (or generates) an <c>X-Correlation-Id</c>, echoes it on the response and pushes it to the Serilog
/// <see cref="LogContext"/>. Incoming values that do not match <c>^[A-Za-z0-9\-_]{1,64}$</c> are ignored
/// to prevent log injection.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    [GeneratedRegex(@"^[A-Za-z0-9\-_]{1,64}$")]
    private static partial Regex ValidCorrelationId();

    public async Task InvokeAsync(HttpContext context)
    {
        string? incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        string correlationId = incoming is not null && ValidCorrelationId().IsMatch(incoming)
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;

        // OnStarting survives the exception handler, which clears response headers.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
