using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MoscoRun.Api.Features.Workouts;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Devices;

namespace MoscoRun.IntegrationTests;

public sealed class AttestedRaceEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Attested_workout_is_visible_only_to_owner_and_late_feedback_cannot_train_the_model()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        using var user = client;
        var (other, _) = await factory.CreateUserClientAsync();
        using var stranger = other;
        var (device, _) = await factory.CreateDeviceClientAsync(user, kind: DeviceKind.PhoneBridge);
        using var phone = device;
        var ct = TestContext.Current.CancellationToken;
        var today = DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime);
        var start = new DateTimeOffset(today.AddDays(-10).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
        var ingest = await phone.PostAsJsonAsync("/api/v1/ingest/workouts", new
        {
            source = "samsung-health",
            sourceId = "race-calibration-test",
            revision = 1,
            activityType = "RUNNING",
            startedAt = start,
            endedAt = start.AddMinutes(25),
            activeDurationSeconds = 1500,
            distanceM = 5000,
        }, ct);
        ingest.EnsureSuccessStatusCode();
        var workout = (await ingest.Content.ReadFromJsonAsync<WorkoutSummaryDto>(ct))!;
        var request = new ScienceFeedbackRequest(null, workout.Id, 1, 1, [new("maximalFlatRun", 1)], null);
        (await user.PutAsJsonAsync("/api/v1/science/feedback", request, ct)).EnsureSuccessStatusCode();
        var uri = $"/api/v1/science/race-calibration?source=samsung-health&from={Date(today.AddDays(-20))}&split={Date(today.AddDays(-5))}&until={Date(today)}&targetMeters=5000";
        var report = (await user.GetFromJsonAsync<AttestedRaceReport>(uri, ct))!;
        report.Model.Should().BeNull();
        report.Prediction.Should().BeNull();
        var evidence = report.Evidence.Should().ContainSingle().Subject;
        evidence.WorkoutId.Should().Be(workout.Id);
        evidence.Role.Should().Be("late-training");
        evidence.DurationSeconds.Should().Be(1500);
        (await stranger.GetFromJsonAsync<AttestedRaceReport>(uri, ct))!.Evidence.Should().BeEmpty();
        (await user.PutAsJsonAsync("/api/v1/science/feedback", request with
        {
            Revision = 2,
            Values = [new("maximalFlatRun", 0)],
        }, ct)).EnsureSuccessStatusCode();
        (await user.GetFromJsonAsync<AttestedRaceReport>(uri, ct))!.Evidence.Should().BeEmpty();
        (await user.GetAsync(uri.Replace("targetMeters=5000", "targetMeters=1", StringComparison.Ordinal), ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string Date(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
