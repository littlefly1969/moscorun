using System.Net;
using System.Net.Http.Json;
using MoscoRun.Api.Features.Dashboard;
using MoscoRun.Api.Features.Devices;

namespace MoscoRun.IntegrationTests;

public sealed class FreshnessTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Latest_weight_survives_a_long_pause_beyond_the_trend_window()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (agent, _) = await factory.CreateDeviceClientAsync(user);
        var measuredAt = factory.Time.GetUtcNow().AddDays(-60);
        (await agent.PostAsJsonAsync("/api/v1/ingest/weighings", new { sourceEventId = "old", measuredAt, weightKg = 81.4m }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var summary = await user.GetFromJsonAsync<DashboardSummary>("/api/v1/dashboard/summary", Json.Options, Ct);

        summary!.LatestWeightKg.Should().Be(81.4m);
        summary.LatestMeasuredAt.Should().BeCloseTo(measuredAt, TimeSpan.FromMilliseconds(1));
        summary.WeighingsLast30Days.Should().Be(0);
        summary.Trend7Days.Should().BeNull();
    }

    [Fact]
    public async Task Contact_and_successful_ingestion_are_tracked_separately()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (agent, registered) = await factory.CreateDeviceClientAsync(user);

        // Richiesta autenticata ma non valida: contatto sì, dati no.
        (await agent.PostAsJsonAsync("/api/v1/ingest/weighings", new { sourceEventId = "bad", measuredAt = factory.Time.GetUtcNow(), weightKg = 900m }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var afterFailure = await GetDeviceAsync(user, registered.Device.Id);
        afterFailure.LastSeenAt.Should().NotBeNull();
        afterFailure.LastIngestedAt.Should().BeNull();

        (await agent.PostAsJsonAsync("/api/v1/ingest/weighings", new { sourceEventId = "ok", measuredAt = factory.Time.GetUtcNow().AddMinutes(-1), weightKg = 80m }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var afterIngest = await GetDeviceAsync(user, registered.Device.Id);
        afterIngest.LastIngestedAt.Should().BeCloseTo(factory.Time.GetUtcNow(), TimeSpan.FromSeconds(1));

        var summary = await user.GetFromJsonAsync<DashboardSummary>("/api/v1/dashboard/summary", Json.Options, Ct);
        summary!.Sources.Single().LastIngestedAt.Should().Be(afterIngest.LastIngestedAt);
    }

    private static async Task<DeviceDto> GetDeviceAsync(HttpClient user, Guid id) =>
        (await user.GetFromJsonAsync<DeviceDto[]>("/api/v1/devices", Json.Options, Ct))!.Single(d => d.Id == id);
}
