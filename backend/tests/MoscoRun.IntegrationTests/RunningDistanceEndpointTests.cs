using System.Net;
using System.Net.Http.Json;
using MoscoRun.Api.Features.Workouts;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Devices;

namespace MoscoRun.IntegrationTests;

public sealed class RunningDistanceEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Incomplete_history_is_not_assumed_rest_and_workout_is_owner_scoped()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        using var user = client;
        var (other, _) = await factory.CreateUserClientAsync();
        using var stranger = other;
        var (device, _) = await factory.CreateDeviceClientAsync(user, kind: DeviceKind.PhoneBridge);
        using var phone = device;
        var ct = TestContext.Current.CancellationToken;
        var start = factory.Time.GetUtcNow().AddHours(-1);
        var ingest = await phone.PostAsJsonAsync("/api/v1/ingest/workouts", new
        {
            source = "samsung-health",
            sourceId = "distance-load-test",
            revision = 1,
            activityType = "RUNNING",
            startedAt = start,
            endedAt = start.AddMinutes(30),
            activeDurationSeconds = 1800,
            distanceM = 6000,
        }, ct);
        ingest.EnsureSuccessStatusCode();
        var workout = (await ingest.Content.ReadFromJsonAsync<WorkoutSummaryDto>(ct))!;
        var uri = $"/api/v1/workouts/{workout.Id}/science/distance-load";
        var report = (await user.GetFromJsonAsync<RunningDistanceReport>(uri, ct))!;
        report.Current.DistanceMeters.Should().Be(6000);
        report.CompleteDays.Should().Be(0);
        report.IncreasePercent.Should().BeNull();
        report.Days.Should().HaveCount(30).And.OnlyContain(day => day.TotalMeters == null);
        (await stranger.GetAsync(uri, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
