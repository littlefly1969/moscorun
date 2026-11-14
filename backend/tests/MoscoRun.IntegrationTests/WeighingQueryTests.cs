using System.Net;
using System.Net.Http.Json;
using MoscoRun.Api.Features.Dashboard;
using MoscoRun.Api.Features.Devices;
using MoscoRun.Api.Features.Weighings;
using MoscoRun.Domain.Measurements;

namespace MoscoRun.IntegrationTests;

public sealed class WeighingQueryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task IngestAsync(HttpClient agent, string id, decimal kg, double daysAgo)
    {
        var response = await agent.PostAsJsonAsync("/api/v1/ingest/weighings", new
        {
            sourceEventId = id,
            measuredAt = factory.Time.GetUtcNow().AddDays(-daysAgo),
            weightKg = kg,
        }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task List_returns_only_own_weighings_newest_first()
    {
        var (alice, _) = await factory.CreateUserClientAsync();
        var (bob, _) = await factory.CreateUserClientAsync();
        var (aliceAgent, _) = await factory.CreateDeviceClientAsync(alice);
        var (bobAgent, _) = await factory.CreateDeviceClientAsync(bob);
        await IngestAsync(aliceAgent, "a1", 80m, 2);
        await IngestAsync(aliceAgent, "a2", 79.5m, 1);
        await IngestAsync(bobAgent, "b1", 60m, 1);

        var list = await alice.GetFromJsonAsync<WeighingList>("/api/v1/weighings", Json.Options, Ct);

        list!.Items.Select(w => w.SourceEventId).Should().Equal("a2", "a1");
    }

    [Fact]
    public async Task List_rejects_inverted_range()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var response = await user.GetAsync("/api/v1/weighings?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Other_users_cannot_touch_my_devices_or_weighings()
    {
        var (alice, _) = await factory.CreateUserClientAsync();
        var (mallory, _) = await factory.CreateUserClientAsync();
        var (agent, device) = await factory.CreateDeviceClientAsync(alice);
        await IngestAsync(agent, "secret", 80m, 1);
        var weighing = (await alice.GetFromJsonAsync<WeighingList>("/api/v1/weighings", Json.Options, Ct))!.Items.Single();

        (await mallory.DeleteAsync($"/api/v1/devices/{device.Device.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await mallory.PutAsJsonAsync($"/api/v1/weighings/{weighing.Id}/attribution", new { attribution = "Rejected" }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await mallory.GetFromJsonAsync<DeviceDto[]>("/api/v1/devices", Json.Options, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Attribution_can_be_changed_and_rejected_weighings_leave_the_summary()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (agent, _) = await factory.CreateDeviceClientAsync(user);
        await IngestAsync(agent, "mine", 80m, 1);
        await IngestAsync(agent, "guest", 55m, 0.5);
        var guest = (await user.GetFromJsonAsync<WeighingList>("/api/v1/weighings", Json.Options, Ct))!.Items.First(w => string.Equals(w.SourceEventId, "guest", StringComparison.Ordinal));

        var update = await user.PutAsJsonAsync($"/api/v1/weighings/{guest.Id}/attribution", new { attribution = "Rejected" }, Ct);
        (await update.Content.ReadFromJsonAsync<WeighingDto>(Json.Options, Ct))!.Attribution.Should().Be(AttributionStatus.Rejected);

        var summary = await user.GetFromJsonAsync<DashboardSummary>("/api/v1/dashboard/summary", Json.Options, Ct);
        summary!.LatestWeightKg.Should().Be(80m);
        summary.WeighingsLast30Days.Should().Be(1);
    }

    [Fact]
    public async Task Summary_reports_trend_and_source_freshness()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (agent, device) = await factory.CreateDeviceClientAsync(user, "Bilancia bagno");
        await IngestAsync(agent, "old1", 82m, 8);
        await IngestAsync(agent, "old2", 81m, 9);
        await IngestAsync(agent, "new1", 80m, 1);
        await IngestAsync(agent, "new2", 79m, 0.1);

        var summary = await user.GetFromJsonAsync<DashboardSummary>("/api/v1/dashboard/summary", Json.Options, Ct);

        summary!.LatestWeightKg.Should().Be(79m);
        summary.Trend7Days.Should().Be(-2m);
        summary.Trend30Days.Should().BeNull();
        summary.WeighingsLast30Days.Should().Be(4);
        summary.Sources.Should().ContainSingle(s => s.DeviceId == device.Device.Id && s.LastSeenAt != null);
    }

    [Fact]
    public async Task Registered_device_token_is_shown_once_and_never_listed()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (_, device) = await factory.CreateDeviceClientAsync(user);
        device.Token.Should().StartWith("mr1_");

        var raw = await user.GetStringAsync("/api/v1/devices", Ct);
        raw.Should().NotContain(device.Token).And.NotContain("token", "l'elenco non espone credenziali né hash");
    }
}
