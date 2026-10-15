using System.Net;

namespace StarterKit.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HealthAndSecurityTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_IsAnonymous()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Responses_CarryCorrelationIdAndSecurityHeaders()
    {
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "test-correlation-123");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal("test-correlation-123", response.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task InvalidCorrelationId_IsReplaced()
    {
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "bad id with spaces!");

        HttpResponseMessage response = await client.SendAsync(request);

        string returned = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.NotEqual("bad id with spaces!", returned);
        Assert.Matches("^[A-Za-z0-9\\-_]{1,64}$", returned);
    }
}
