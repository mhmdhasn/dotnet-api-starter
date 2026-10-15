using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StarterKit.Api.Common.Errors;
using StarterKit.Api.Domain;
using StarterKit.Api.Persistence;

namespace StarterKit.Api.Auth;

public sealed class AuthService(
    UserManager<AppUser> userManager,
    AppDbContext db,
    ITokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            // Note: this reveals that an email is registered (account enumeration). See docs/package-decisions.md.
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        IdentityResult created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new AppException(
                StatusCodes.Status400BadRequest,
                "Registration failed",
                string.Join(" ", created.Errors.Select(error => error.Description)));
        }

        // The "User" role must exist: enable Database:SeedOnStartup once, or run DbSeeder (see README).
        await userManager.AddToRoleAsync(user, Roles.User);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        // Same generic failure for unknown user, wrong password and lockout.
        AppUser? user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            throw InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw InvalidCredentials();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            throw InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        string hash = tokens.HashToken(request.RefreshToken);
        DateTimeOffset now = timeProvider.GetUtcNow();

        RefreshToken? stored = await db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            throw InvalidRefreshToken();
        }

        if (stored.RevokedAt is not null)
        {
            // A revoked token was presented again: assume theft and revoke every active token of this user.
            await RevokeAllForUserAsync(stored.UserId, now, cancellationToken);
            throw InvalidRefreshToken();
        }

        if (!stored.IsActive(now))
        {
            throw InvalidRefreshToken();
        }

        AppUser? user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            await RevokeAllForUserAsync(stored.UserId, now, cancellationToken);
            throw InvalidRefreshToken();
        }

        // Single-use rotation: the conditional update succeeds for exactly one concurrent caller.
        string newRefreshToken = tokens.GenerateRefreshToken();
        string newHash = tokens.HashToken(newRefreshToken);

        int claimed = await db.RefreshTokens
            .Where(token => token.Id == stored.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, (DateTimeOffset?)now)
                    .SetProperty(token => token.ReplacedByTokenHash, newHash),
                cancellationToken);

        if (claimed == 0)
        {
            await RevokeAllForUserAsync(stored.UserId, now, cancellationToken);
            throw InvalidRefreshToken();
        }

        IList<string> roles = await userManager.GetRolesAsync(user);
        AccessToken access = tokens.CreateAccessToken(user, roles);
        DateTimeOffset refreshExpires = await StoreRefreshTokenAsync(user.Id, newHash, now, cancellationToken);

        return new AuthResponse(access.Token, access.ExpiresAt, newRefreshToken, refreshExpires);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        string hash = tokens.HashToken(request.RefreshToken);
        DateTimeOffset now = timeProvider.GetUtcNow();

        // Idempotent: unknown or already revoked tokens are silently ignored.
        await db.RefreshTokens
            .Where(token => token.TokenHash == hash && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, (DateTimeOffset?)now),
                cancellationToken);
    }

    public async Task<UserResponse?> GetUserAsync(Guid userId)
    {
        AppUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        IList<string> roles = await userManager.GetRolesAsync(user);
        return new UserResponse(user.Id, user.Email ?? string.Empty, user.DisplayName, roles.ToList());
    }

    private async Task<AuthResponse> IssueTokensAsync(AppUser user, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        IList<string> roles = await userManager.GetRolesAsync(user);
        AccessToken access = tokens.CreateAccessToken(user, roles);

        string refreshToken = tokens.GenerateRefreshToken();
        DateTimeOffset refreshExpires = await StoreRefreshTokenAsync(
            user.Id, tokens.HashToken(refreshToken), now, cancellationToken);

        return new AuthResponse(access.Token, access.ExpiresAt, refreshToken, refreshExpires);
    }

    private async Task<DateTimeOffset> StoreRefreshTokenAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DateTimeOffset expiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        });

        await db.SaveChangesAsync(cancellationToken);
        return expiresAt;
    }

    private Task<int> RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
        => db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, (DateTimeOffset?)now),
                cancellationToken);

    private static AppException InvalidCredentials()
        => new(StatusCodes.Status401Unauthorized, "Unauthorized", "Invalid email or password.");

    private static AppException InvalidRefreshToken()
        => new(StatusCodes.Status401Unauthorized, "Unauthorized", "The refresh token is invalid or has expired.");
}
