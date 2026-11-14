using System.Net;
using System.Net.Http.Json;
using MoscoRun.Api.Features.System;

namespace MoscoRun.IntegrationTests;

public sealed class PlatformTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_are_healthy(string path)
    {
        var response = await factory.CreateClient().GetAsync(path, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task System_info_is_public_and_reports_version()
    {
        var info = await factory.CreateClient().GetFromJsonAsync<SystemInfo>("/api/system/info", Ct);
        info!.Version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Unknown_api_route_is_404_not_spa_fallback()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/does-not-exist", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("/api/system/info", Ct);
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task OpenApi_contract_is_served()
    {
        var response = await factory.CreateClient().GetAsync("/api/openapi/v1.json", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("/api/v1/ingest/weighings");
    }
}
