using StarterKit.Api.Common.Endpoints;
using StarterKit.Api.Common.Errors;
using StarterKit.Api.Common.Security;
using StarterKit.Api.Common.Validation;

namespace StarterKit.Api.Auth;

public sealed class AuthEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, AuthService auth, CancellationToken ct) =>
                Results.Created("/api/auth/me", await auth.RegisterAsync(request, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithValidation<RegisterRequest>()
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", async (LoginRequest request, AuthService auth, CancellationToken ct) =>
                Results.Ok(await auth.LoginAsync(request, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithValidation<LoginRequest>()
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", async (RefreshRequest request, AuthService auth, CancellationToken ct) =>
                Results.Ok(await auth.RefreshAsync(request, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithValidation<RefreshRequest>()
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (LogoutRequest request, AuthService auth, CancellationToken ct) =>
            {
                await auth.LogoutAsync(request, ct);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithValidation<LogoutRequest>()
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", async (ICurrentUser currentUser, AuthService auth) =>
            {
                if (currentUser.UserId is not { } userId)
                {
                    throw new AppException(StatusCodes.Status401Unauthorized, "Unauthorized", "Not authenticated.");
                }

                UserResponse? user = await auth.GetUserAsync(userId);
                return user is null ? Results.Unauthorized() : Results.Ok(user);
            })
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
