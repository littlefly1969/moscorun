using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MoscoRun.Api.Features.Workouts;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Devices;

namespace MoscoRun.IntegrationTests;

public sealed class ScienceFeedbackEndpointTests(ApiFactory factory)
{
    private const string Endpoint = "/api/v1/science/feedback";

    [Fact]
    public async Task Daily_feedback_preserves_revisions_null_and_zero_and_feeds_longitudinal_analysis()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        using var user = client;
        var (other, _) = await factory.CreateUserClientAsync();
        using var stranger = other;
        var ct = TestContext.Current.CancellationToken;
        var day = DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime).AddDays(-1);
        var uri = Endpoint + "?day=" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var empty = await user.GetFromJsonAsync<ScienceFeedbackReport>(uri, ct);
        empty!.Revision.Should().BeNull();
        var request = new ScienceFeedbackRequest(day, null, null, 1, [new("fatigue", 0), new("sleepQuality", 8)], "Nota");
        var saved = await SaveAsync(user, request, ct);
        saved.Revision.Should().Be(1);
        saved.Values.Single(v => string.Equals(v.Metric, "fatigue", StringComparison.Ordinal)).Value.Should().Be(0);
        saved.Note.Should().Be("Nota");
        (await SaveAsync(user, request, ct)).Revision.Should().Be(1);
        (await user.PutAsJsonAsync(Endpoint, request with { Note = "Conflitto" }, ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await user.PutAsJsonAsync(Endpoint, request with { Revision = 3 }, ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var correction = request with { Revision = 2, Values = [new("sleepQuality", 7)], Note = null };
        var corrected = await SaveAsync(user, correction, ct);
        corrected.Revision.Should().Be(2);
        corrected.Values.Single(v => string.Equals(v.Metric, "fatigue", StringComparison.Ordinal)).Value.Should().BeNull();
        var dailyUri = $"/api/v1/science/daily?metric=sleepQuality&source=athlete-feedback&day={day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&baselineDays=7";
        var daily = await user.GetFromJsonAsync<DailyMetricReport>(dailyUri, ct);
        daily!.Current.Value.Should().Be(7);
        var isolated = await stranger.GetFromJsonAsync<ScienceFeedbackReport>(uri, ct);
        isolated!.Revision.Should().BeNull();
        isolated.Values.Should().OnlyContain(v => v.Value == null);
    }

    [Fact]
    public async Task Workout_feedback_is_bound_to_revision_owner_and_active_duration()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        using var user = client;
        var (other, _) = await factory.CreateUserClientAsync();
        using var stranger = other;
        var (device, _) = await factory.CreateDeviceClientAsync(user, kind: DeviceKind.PhoneBridge);
        using var phone = device;
        var ct = TestContext.Current.CancellationToken;
        var id = await IngestAsync(phone, 1, ct);
        var uri = Endpoint + "?workoutId=" + id;
        var empty = await user.GetFromJsonAsync<ScienceFeedbackReport>(uri, ct);
        empty!.SessionRpeLoad.Should().BeNull();
        var request = new ScienceFeedbackRequest(null, id, 1, 1, [new("rpe", 7)], null);
        var saved = await SaveAsync(user, request, ct);
        saved.ActiveDurationSeconds.Should().Be(1800);
        saved.SessionRpeLoad.Should().Be(210);
        (await stranger.GetAsync(uri, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PutAsJsonAsync(Endpoint, request, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await user.PutAsJsonAsync(Endpoint, request with { Revision = 2, Values = [new("rpe", 11)] }, ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await IngestAsync(phone, 2, ct)).Should().Be(id);
        var revised = await user.GetFromJsonAsync<ScienceFeedbackReport>(uri, ct);
        revised!.WorkoutRevision.Should().Be(2);
        revised.SessionRpeLoad.Should().BeNull();
        (await user.PutAsJsonAsync(Endpoint, request, ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<Guid> IngestAsync(HttpClient phone, int revision, CancellationToken ct)
    {
        var end = factory.Time.GetUtcNow().AddHours(-1);
        var response = await phone.PostAsJsonAsync("/api/v1/ingest/workouts", new
        {
            source = "samsung-health",
            sourceId = "feedback-session",
            revision,
            activityType = "RUNNING",
            startedAt = end.AddHours(-1),
            endedAt = end,
            utcOffsetMinutes = 0,
            activeDurationSeconds = 1800,
            distanceM = 5000 + revision,
        }, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkoutSummaryDto>(ct))!.Id;
    }

    private static async Task<ScienceFeedbackReport> SaveAsync(HttpClient client, ScienceFeedbackRequest request, CancellationToken ct)
    {
        var response = await client.PutAsJsonAsync(Endpoint, request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ScienceFeedbackReport>(ct))!;
    }
}
