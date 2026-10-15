using Microsoft.AspNetCore.Cors.Infrastructure;

namespace StarterKit.Api.Common.Security;

public static class CorsExtensions
{
    /// <summary>
    /// Registers CORS from <c>Cors:AllowedOrigins</c>. With no origins configured (the default)
    /// no cross-origin request is allowed.
    /// </summary>
    public static IServiceCollection AddCorsFromConfiguration(this IServiceCollection services)
    {
        services.AddCors();

        services.AddOptions<CorsOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                string[] origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

                options.AddDefaultPolicy(policy =>
                {
                    if (origins.Length > 0)
                    {
                        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
                    }
                });
            });

        return services;
    }
}
