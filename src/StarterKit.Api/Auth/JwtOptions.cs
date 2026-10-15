using System.ComponentModel.DataAnnotations;

namespace StarterKit.Api.Auth;

/// <summary>
/// JWT settings. <see cref="Key"/> must NEVER be committed: use user-secrets locally (the setup script does this)
/// and the <c>Jwt__Key</c> environment variable / a secret store in production.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "StarterKit";

    [Required]
    public string Audience { get; set; } = "StarterKit.Clients";

    [Required, MinLength(32)]
    public string Key { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;
}
