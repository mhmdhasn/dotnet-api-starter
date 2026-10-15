using FluentValidation.Results;
using StarterKit.Api.Auth;

namespace StarterKit.UnitTests;

public class AuthValidatorTests
{
    private readonly RegisterRequestValidator _register = new();
    private readonly LoginRequestValidator _login = new();
    private readonly RefreshRequestValidator _refresh = new();
    private readonly LogoutRequestValidator _logout = new();

    [Fact]
    public void Register_WithValidData_Passes()
    {
        ValidationResult result = _register.Validate(new RegisterRequest("user@example.com", "Passw0rdOK", "User"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Register_WithBadEmail_FailsOnEmail(string email)
    {
        ValidationResult result = _register.Validate(new RegisterRequest(email, "Passw0rdOK", null));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterRequest.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sh0rt")]
    [InlineData("nouppercase1")]
    [InlineData("NOLOWERCASE1")]
    [InlineData("NoDigitsHere")]
    public void Register_WithWeakPassword_FailsOnPassword(string password)
    {
        ValidationResult result = _register.Validate(new RegisterRequest("user@example.com", password, null));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void Register_WithTooLongDisplayName_Fails()
    {
        ValidationResult result = _register.Validate(
            new RegisterRequest("user@example.com", "Passw0rdOK", new string('x', 101)));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterRequest.DisplayName));
    }

    [Fact]
    public void Login_RequiresEmailAndPassword()
    {
        ValidationResult result = _login.Validate(new LoginRequest("", ""));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LoginRequest.Email));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LoginRequest.Password));
    }

    [Fact]
    public void Refresh_And_Logout_RequireToken()
    {
        Assert.False(_refresh.Validate(new RefreshRequest("")).IsValid);
        Assert.False(_logout.Validate(new LogoutRequest("")).IsValid);
        Assert.True(_refresh.Validate(new RefreshRequest("abc")).IsValid);
        Assert.True(_logout.Validate(new LogoutRequest("abc")).IsValid);
    }
}
