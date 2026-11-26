using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoscoRun.Api.Features.Workouts;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Devices;

namespace MoscoRun.IntegrationTests;

public sealed class WorkoutIntensityEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Native_series_are_analyzed_only_for_the_authenticated_owner()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        using var user = client;
        var (other, _) = await factory.CreateUserClientAsync();
        using var stranger = other;
        var (device, _) = await factory.CreateDeviceClientAsync(user, kind: DeviceKind.PhoneBridge);
        using var phone = device;
        var ct = TestContext.Current.CancellationToken;
        var start = DateTimeOffset.FromUnixTimeMilliseconds(factory.Time.GetUtcNow().AddHours(-1).ToUnixTimeMilliseconds());
        var ingest = await phone.PostAsJsonAsync("/api/v1/ingest/workouts", new
        {
            source = "samsung-health",
            sourceId = "intensity-session",
            revision = 1,
            activityType = "RUNNING",
            startedAt = start,
            endedAt = start.AddSeconds(20),
            utcOffsetMinutes = 0,
            activeDurationSeconds = 20,
            distanceM = 80,
            series = new[]
            {
                new
                {
                    metric = "HeartRate",
                    samples = Enumerable.Range(0, 5).Select(i => new { time = start.AddSeconds(i * 5.0), value = 140 }),
                },
            },
        }, ct);
        ingest.EnsureSuccessStatusCode();
        var workout = (await ingest.Content.ReadFromJsonAsync<WorkoutSummaryDto>(ct))!;
        var uri = $"/api/v1/workouts/{workout.Id}/science/intensity?metric=HeartRate&thresholds=130,150";
        var result = await user.GetAsync(uri, ct);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Headers.CacheControl!.NoStore.Should().BeTrue();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var report = (await result.Content.ReadFromJsonAsync<WorkoutIntensityReport>(options, ct))!;
        report.Distribution.Coverage.Should().BeApproximately(1, 1e-9);
        report.Distribution.ZoneMinutes[1].Should().BeApproximately(20d / 60, 1e-9);
        report.ObservedMean.Should().Be(140);
        (await stranger.GetAsync(uri, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await user.GetAsync(uri.Replace("130,150", "150,130", StringComparison.Ordinal), ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
