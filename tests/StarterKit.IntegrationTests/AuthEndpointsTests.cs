using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StarterKit.Api.Auth;

namespace StarterKit.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthEndpointsTests(ApiFactory factory)
{
    private const string StrongPassword = "Passw0rdOK";

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<AuthResponse> ReadAuthAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

    [Fact]
    public async Task Register_WithInvalidData_Returns400WithFieldErrors()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("not-an-email", "weak", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Http.HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(problem.Errors.Keys, key => string.Equals(key, "Email", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(problem.Errors.Keys, key => string.Equals(key, "Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_Login_Me_Works()
    {
        using HttpClient client = factory.CreateClient();
        string email = NewEmail();

        HttpResponseMessage registered = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest(email, StrongPassword, "Test User"));
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        HttpResponseMessage loggedIn = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, StrongPassword));
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
        AuthResponse tokens = await ReadAuthAsync(loggedIn);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        HttpResponseMessage me = await client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await me.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Contains("User", user.Roles);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        using HttpClient client = factory.CreateClient();
        string email = NewEmail();
        var request = new RegisterRequest(email, StrongPassword, null);

        await client.PostAsJsonAsync("/api/auth/register", request);
        HttpResponseMessage second = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        using HttpClient client = factory.CreateClient();
        string email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, StrongPassword, null));

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, "WrongPassw0rd"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(NewEmail(), StrongPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndReuseRevokesTheWholeChain()
    {
        using HttpClient client = factory.CreateClient();
        string email = NewEmail();
        HttpResponseMessage registered = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest(email, StrongPassword, null));
        AuthResponse first = await ReadAuthAsync(registered);

        // 1) Rotation: the first refresh token yields a new, different pair.
        HttpResponseMessage rotated = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(first.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        AuthResponse second = await ReadAuthAsync(rotated);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        // 2) Reuse of the already-rotated token is rejected...
        HttpResponseMessage reused = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(first.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        // 3) ...and it revokes the legitimate newest token too.
        HttpResponseMessage chainRevoked = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(second.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, chainRevoked.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        using HttpClient client = factory.CreateClient();
        string email = NewEmail();
        HttpResponseMessage registered = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest(email, StrongPassword, null));
        AuthResponse tokens = await ReadAuthAsync(registered);

        HttpResponseMessage logout = await client.PostAsJsonAsync(
            "/api/auth/logout", new LogoutRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        HttpResponseMessage refresh = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
