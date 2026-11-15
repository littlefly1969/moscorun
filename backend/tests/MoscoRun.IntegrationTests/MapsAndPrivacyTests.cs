using System.Net;
using System.Net.Http.Json;
using MoscoRun.Api.Features.Maps;
using MoscoRun.Api.Features.Workouts;
using MoscoRun.Domain.Devices;

namespace MoscoRun.IntegrationTests;

public sealed class MapsAndPrivacyTests(ApiFactory factory)
{
    // Chiave finta di forma valida, spezzata in due: nessun letterale simile a una chiave nel repository (gitleaks).
    private const string GoogleKey = "AIza" + "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Without_configuration_there_is_no_map_provider_and_csp_stays_strict()
    {
        var (user, _) = await factory.CreateUserClientAsync();

        var config = await user.GetFromJsonAsync<MapsConfigDto>("/api/v1/maps/config", Json.Options, Ct);
        var page = await user.GetAsync("/api/system/info", Ct);

        config.Should().Be(new MapsConfigDto("none", null));
        page.Headers.GetValues("Content-Security-Policy").Single().Should().NotContain("google");
        page.Headers.GetValues("Referrer-Policy").Single().Should().Be("no-referrer");
    }

    [Fact]
    public async Task Google_maps_key_is_served_and_csp_allows_only_google_domains()
    {
        await using var google = factory.WithWebHostBuilder(b => b.UseSetting("Maps:GoogleApiKey", GoogleKey));
        var (email, _) = await factory.CreateUserAsync();
        var user = google.CreateClient();
        (await user.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.UserPassword }, Ct)).EnsureSuccessStatusCode();

        var config = await user.GetFromJsonAsync<MapsConfigDto>("/api/v1/maps/config", Json.Options, Ct);
        var page = await user.GetAsync("/api/system/info", Ct);

        config.Should().Be(new MapsConfigDto("google", GoogleKey));
        var csp = page.Headers.GetValues("Content-Security-Policy").Single();
        csp.Should().Contain("https://*.googleapis.com").And.Contain("frame-ancestors 'none'").And.NotContain("unsafe-eval");
        page.Headers.GetValues("Referrer-Policy").Single().Should().Be("strict-origin", "la restrizione per referrer della chiave richiede l'origine");
        (await google.CreateClient().GetAsync("/api/v1/maps/config", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void Invalid_map_configuration_prevents_startup()
    {
        using var broken = factory.WithWebHostBuilder(b => b.UseSetting("Maps:Provider", "google").UseSetting("Maps:GoogleApiKey", "sbagliata"));

        var act = () => broken.CreateClient();

        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>().WithMessage("*GoogleApiKey*");
    }

    [Fact]
    public async Task Privacy_zones_hide_start_and_end_and_the_full_bounds()
    {
        // Il tracciato di prova parte da 41.89,12.49 e sale di 2e-5 gradi al secondo (circa 2,2 m).
        await using var withZones = factory.WithWebHostBuilder(b => b.UseSetting("Privacy:RouteZones", "41.89,12.49,100"));
        var (email, _) = await factory.CreateUserAsync();
        var user = withZones.CreateClient();
        (await user.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.UserPassword }, Ct)).EnsureSuccessStatusCode();
        var (phone, _) = await factory.CreateDeviceClientAsync(user, "Telefono", DeviceKind.PhoneBridge);
        var created = await phone.PostAsJsonAsync(
            "/api/v1/ingest/workouts", WorkoutIngestionTests.Payload(factory.Time.GetUtcNow().AddDays(-1), sourceId: "privacy:0", points: 600), Ct);
        var workout = (await created.Content.ReadFromJsonAsync<WorkoutSummaryDto>(Json.Options, Ct))!;

        var route = await user.GetFromJsonAsync<RouteFeature>($"/api/v1/workouts/{workout.Id}/route?maxPoints=100000", Json.Options, Ct);
        var list = await user.GetFromJsonAsync<WorkoutList>("/api/v1/workouts", Json.Options, Ct);

        var detail = await user.GetFromJsonAsync<WorkoutDetailDto>($"/api/v1/workouts/{workout.Id}", Json.Options, Ct);
        detail!.Summary.Bounds.Should().BeNull("il riquadro completo includerebbe la zona privata");
        route!.Properties.HiddenStartPoints.Should().BeInRange(30, 60);
        route.Properties.HiddenEndPoints.Should().Be(0);
        route.Properties.ReturnedPoints.Should().Be(600 - route.Properties.HiddenStartPoints);
        route.Bbox[1].Should().BeGreaterThan(41.89 + 0.0005, "il riquadro restituito non include la partenza nascosta");
        list!.Items.Should().OnlyContain(w => w.Bounds == null);
    }
}
