using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StarterKit.Api.Auth;
using StarterKit.Api.Common.Endpoints;
using StarterKit.Api.Common.Errors;
using StarterKit.Api.Common.Middleware;
using StarterKit.Api.Common.Security;
using StarterKit.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// Logging. Configured from the "Serilog" section. Do NOT use CreateBootstrapLogger here: it breaks
// tests that create several WebApplicationFactory instances.
// ---------------------------------------------------------------------------------------------
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

// ---------------------------------------------------------------------------------------------
// Services. Nothing below reads configuration eagerly, so test/environment overrides always apply.
// ---------------------------------------------------------------------------------------------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddEndpoints(typeof(Program).Assembly);

builder.Services.AddCorsFromConfiguration();
builder.Services.AddRateLimiting();
builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The integration tests (environment "Testing") register their own SQLite provider.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddAppDbContext(options =>
    {
        string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured (set ConnectionStrings__DefaultConnection).");
        }

        options.UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure());
    });
}

builder.Services.AddAuth();

var app = builder.Build();

// ---------------------------------------------------------------------------------------------
// Pipeline. Order matters.
// ---------------------------------------------------------------------------------------------
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapEndpoints();

await app.InitializeDatabaseAsync();

app.Run();

// Required so WebApplicationFactory<Program> can see the entry point from the test projects.
public partial class Program { }
