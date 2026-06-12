using System.Net;
using System.Net.Http.Json;
using ExpenseReports.Application.Auth;
using ExpenseReports.Infrastructure.Persistence;

namespace ExpenseReports.IntegrationTests;

public sealed class AuthTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Login_with_seeded_credentials_returns_a_token()
    {
        var client = AnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            email = "alice@acme.test",
            password = DatabaseSeeder.DemoPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(login!.AccessToken));
        Assert.True(login.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("alice@acme.test", "wrong-password")]
    [InlineData("ghost@nowhere.test", "Passw0rd!demo")]
    public async Task Login_with_bad_credentials_is_401_with_uniform_message(string email, string password)
    {
        var client = AnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid e-mail or password", body);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_token()
    {
        var client = AnonymousClient();

        var response = await client.GetAsync("/expenses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Healthcheck_is_anonymous()
    {
        var response = await AnonymousClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
