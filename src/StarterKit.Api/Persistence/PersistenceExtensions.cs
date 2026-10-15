using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace StarterKit.Api.Persistence;

public static class PersistenceExtensions
{
    /// <summary>
    /// Registers <see cref="AppDbContext"/> with the audit interceptor. The database provider is chosen by the
    /// caller (SQL Server in the app, SQLite in integration tests).
    /// </summary>
    public static IServiceCollection AddAppDbContext(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureProvider)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            configureProvider(options);
            options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        return services;
    }

    /// <summary>
    /// Applies migrations and/or runs the seeder according to <c>Database:MigrateOnStartup</c> and
    /// <c>Database:SeedOnStartup</c> (both true only in Development by default).
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        bool migrate = app.Configuration.GetValue<bool>("Database:MigrateOnStartup");
        bool seed = app.Configuration.GetValue<bool>("Database:SeedOnStartup");

        if (!migrate && !seed)
        {
            return;
        }

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

        if (migrate)
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        if (seed)
        {
            await DbSeeder.SeedAsync(scope.ServiceProvider);
        }
    }
}
