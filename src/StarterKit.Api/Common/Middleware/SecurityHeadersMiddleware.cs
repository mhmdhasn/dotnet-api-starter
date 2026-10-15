namespace StarterKit.Api.Common.Middleware;

/// <summary>
/// Adds defensive HTTP response headers. The strict Content-Security-Policy is skipped in Development
/// so that the Swagger UI keeps working.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    private readonly bool _isDevelopment = environment.IsDevelopment();

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";

            if (!_isDevelopment)
            {
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
