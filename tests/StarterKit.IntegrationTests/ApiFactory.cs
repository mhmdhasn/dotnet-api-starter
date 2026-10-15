using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StarterKit.Api.Persistence;

namespace StarterKit.IntegrationTests;

/// <summary>
/// Hosts the real API on a shared in-memory SQLite database. NOTE: SQLite is not SQL Server; behaviour that depends
/// on the provider (collations, concurrency tokens, SQL translation) needs Testcontainers.MsSql, see the README.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // An in-memory SQLite database lives only as long as its connection, so keep one open for the factory's lifetime.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "integration-tests-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz",
                ["RateLimiting:GlobalPermitLimit"] = "1000000",
                ["RateLimiting:AuthPermitLimit"] = "1000000"
            });
        });

        builder.ConfigureTestServices(services =>
            services.AddAppDbContext(options => options.UseSqlite(_connection)));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        IHost host = base.CreateHost(builder);

        using IServiceScope scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        DbSeeder.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>One shared factory (and therefore one host) for all integration tests.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
