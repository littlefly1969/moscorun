using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using MoscoRun.Api.Auth;

namespace MoscoRun.IntegrationTests;

public sealed class AuthTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Bootstrap_admin_can_log_in_and_out()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword }, Ct);
        login.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".MoscoRun.Session=", StringComparison.Ordinal));
        cookie.Should().Contain("httponly").And.Contain("samesite=strict");

        var me = await client.GetFromJsonAsync<CurrentUser>("/api/auth/me", Ct);
        me!.Email.Should().Be(ApiFactory.AdminEmail);

        (await client.PostAsync("/api/auth/logout", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_without_revealing_user_existence()
    {
        var client = factory.CreateClient();
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.AdminEmail, password = "wrong-password-123" }, Ct);
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@moscorun.test", password = "wrong-password-123" }, Ct);

        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var a = await wrong.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        var b = await unknown.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        a!.Detail.Should().Be(b!.Detail);
        a.Title.Should().Be(b.Title);
    }

    [Fact]
    public async Task Login_attempts_are_rate_limited()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("Security:LoginAttemptsPerMinute", "3"));
        var client = limited.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            var r = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@moscorun.test", password = "wrong-password-123" }, Ct);
            statuses.Add(r.StatusCode);
        }

        statuses.Should().Equal(
            HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
            HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Invalid_login_payload_is_a_validation_problem()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "not-an-email", password = "" }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/v1/weighings")]
    [InlineData("/api/v1/devices")]
    [InlineData("/api/v1/dashboard/summary")]
    public async Task Protected_endpoints_return_401_without_redirect(string path)
    {
        var response = await factory.CreateClient().GetAsync(path, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
    }
}
