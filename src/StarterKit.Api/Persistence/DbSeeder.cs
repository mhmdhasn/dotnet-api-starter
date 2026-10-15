using Microsoft.AspNetCore.Identity;
using StarterKit.Api.Auth;
using StarterKit.Api.Domain;

namespace StarterKit.Api.Persistence;

/// <summary>
/// Idempotent seeding: creates the Admin/User roles and, when <c>Seed:AdminEmail</c> and <c>Seed:AdminPassword</c>
/// are both configured (use user-secrets or environment variables, never source control), an administrator account.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));

        foreach (string role in new[] { Roles.Admin, Roles.User })
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            IdentityResult created = await roleManager.CreateAsync(
                new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() });

            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create role '{role}': {string.Join("; ", created.Errors.Select(e => e.Description))}");
            }
        }

        string? adminEmail = configuration["Seed:AdminEmail"];
        string? adminPassword = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        if (await userManager.FindByEmailAsync(adminEmail) is not null)
        {
            return;
        }

        var admin = new AppUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            DisplayName = "Administrator"
        };

        IdentityResult result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
        {
            logger.LogError(
                "Could not seed the admin user: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRolesAsync(admin, [Roles.Admin, Roles.User]);
        logger.LogInformation("Seeded admin user {Email}.", adminEmail);
    }
}
