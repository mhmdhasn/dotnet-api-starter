using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StarterKit.Api.Auth;
using StarterKit.Api.Domain;
using System.Text;

namespace StarterKit.UnitTests;

public class TokenServiceTests
{
    private const string SigningKey = "unit-test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";

    private static TokenService CreateService(string key = SigningKey)
        => new(
            Options.Create(new JwtOptions { Key = key, Issuer = "issuer", Audience = "audience", AccessTokenMinutes = 15 }),
            TimeProvider.System);

    private static TokenValidationParameters ValidationParameters(string key) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "issuer",
        ValidateAudience = true,
        ValidAudience = "audience",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = true
    };

    [Fact]
    public async Task CreateAccessToken_ProducesValidTokenWithSubAndRole()
    {
        var user = new AppUser { Email = "user@example.com", UserName = "user@example.com" };
        AccessToken token = CreateService().CreateAccessToken(user, ["Admin"]);

        TokenValidationResult validation = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Token, ValidationParameters(SigningKey));
        Assert.True(validation.IsValid);

        var jwt = new JsonWebToken(token.Token);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Contains(jwt.Claims, claim => claim.Type == "role" && claim.Value == "Admin");
        Assert.Contains(jwt.Claims, claim => claim.Type == "email" && claim.Value == "user@example.com");
        Assert.True(token.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Token_SignedWithAnotherKey_IsRejected()
    {
        var user = new AppUser { Email = "user@example.com" };
        AccessToken token = CreateService().CreateAccessToken(user, ["User"]);

        TokenValidationResult validation = await new JsonWebTokenHandler().ValidateTokenAsync(
            token.Token,
            ValidationParameters("a-completely-different-key-0123456789-abcdefghijklmnop"));

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void RefreshTokens_AreUnique_AndOnlyTheHashIsDerivable()
    {
        TokenService service = CreateService();

        string first = service.GenerateRefreshToken();
        string second = service.GenerateRefreshToken();

        Assert.NotEqual(first, second);
        Assert.True(first.Length >= 80, "64 random bytes should encode to at least 86 Base64Url characters.");

        string hash = service.HashToken(first);
        Assert.Equal(64, hash.Length);
        Assert.NotEqual(first, hash);
        Assert.Equal(hash, service.HashToken(first));
        Assert.NotEqual(hash, service.HashToken(second));
    }
}
