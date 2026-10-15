using StarterKit.Api.Domain;

namespace StarterKit.Api.Auth;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    /// <summary>Creates a signed HS256 access token with the claims sub, jti, email and role.</summary>
    AccessToken CreateAccessToken(AppUser user, IEnumerable<string> roles);

    /// <summary>Generates a new opaque refresh token (64 random bytes, Base64Url). Store only its hash.</summary>
    string GenerateRefreshToken();

    /// <summary>Hex-encoded SHA-256 of a refresh token; this is what is persisted.</summary>
    string HashToken(string token);
}
