using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace StarterKit.Api.Common.Security;

public static class RateLimitPolicies
{
    /// <summary>Stricter policy for anonymous authentication endpoints.</summary>
    public const string Auth = "auth";
}

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    [Range(1, int.MaxValue)]
    public int GlobalPermitLimit { get; set; } = 100;

    [Range(1, int.MaxValue)]
    public int GlobalWindowSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int AuthPermitLimit { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int AuthWindowSeconds { get; set; } = 60;
}

public static class RateLimitingExtensions
{
    /// <summary>
    /// Global per-IP fixed window plus a stricter "auth" policy. Settings are bound lazily through
    /// <see cref="IOptions{TOptions}"/> so tests and environments can override configuration.
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingSettings>()
            .BindConfiguration(RateLimitingSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingSettings>>((options, settingsAccessor) =>
            {
                RateLimitingSettings settings = settingsAccessor.Value;

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        GetClientKey(context),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.GlobalPermitLimit,
                            Window = TimeSpan.FromSeconds(settings.GlobalWindowSeconds),
                            QueueLimit = 0
                        }));

                options.AddPolicy(RateLimitPolicies.Auth, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        GetClientKey(context),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.AuthPermitLimit,
                            Window = TimeSpan.FromSeconds(settings.AuthWindowSeconds),
                            QueueLimit = 0
                        }));

                options.OnRejected = async (rejected, _) =>
                {
                    HttpContext http = rejected.HttpContext;
                    http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                    if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                    {
                        http.Response.Headers.RetryAfter =
                            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                    }

                    var problemDetails = http.RequestServices.GetRequiredService<IProblemDetailsService>();
                    await problemDetails.TryWriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = http,
                        ProblemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too many requests",
                            Detail = "Rate limit exceeded. Try again later."
                        }
                    });
                };
            });

        return services;
    }

    // Behind a reverse proxy configure ForwardedHeaders so RemoteIpAddress is the real client (see README).
    private static string GetClientKey(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
