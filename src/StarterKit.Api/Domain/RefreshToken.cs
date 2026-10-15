namespace StarterKit.Api.Domain;

/// <summary>
/// Server-side record of an issued refresh token. Only the SHA-256 hash of the token is stored, never the token itself.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Hex-encoded SHA-256 of the raw token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Hash of the token that replaced this one when it was rotated.</summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>Evaluated in memory on purpose: SQLite cannot translate DateTimeOffset comparisons.</summary>
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
