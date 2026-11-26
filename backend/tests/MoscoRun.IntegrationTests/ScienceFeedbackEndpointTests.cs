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

    [Fact]
    public async Task Daily_load_distinguishes_rest_missing_and_unrated_sessions()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        using var user = client;
        var (other, _) = await factory.CreateUserClientAsync();
        using var stranger = other;
        var ct = TestContext.Current.CancellationToken;
        var today = DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime);
        var last = today.AddDays(-1);
        var uri = "/api/v1/science/load?last=" + last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var request = new ScienceFeedbackRequest(last, null, null, 1, [new("trainingDayComplete", 1)], null);
        var missing = await user.GetFromJsonAsync<DailySessionLoadReport>(uri, ct);
        missing!.Days[^1].Load.Should().BeNull();
        (await user.PutAsJsonAsync(Endpoint, request with { Day = today }, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await user.PutAsJsonAsync(Endpoint, request with { Values = [new("trainingDayComplete", 0.5)] }, ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SaveAsync(user, request, ct);
        var rest = await user.GetFromJsonAsync<DailySessionLoadReport>(uri, ct);
        rest!.Days[^1].Status.Should().Be("observed-rest");
        rest.Days[^1].Load.Should().Be(0);
        rest.RecentWeek.Should().BeNull();
        var isolated = await stranger.GetFromJsonAsync<DailySessionLoadReport>(uri, ct);
        isolated!.Days[^1].Load.Should().BeNull();
        await SaveAsync(user, request with { Revision = 2, Values = [new("trainingDayComplete", 0)] }, ct);
        var revoked = await user.GetFromJsonAsync<DailySessionLoadReport>(uri, ct);
        revoked!.Days[^1].Load.Should().BeNull();
    }

    [Fact]
    public async Task Nutrition_is_calculated_from_saved_complete_revision_only()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        using var user = client;
        var ct = TestContext.Current.CancellationToken;
        var today = DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime);
        var day = today.AddDays(-1);
        ScienceFeedbackInput[] values =
        [
            new("nutritionDiaryComplete", 1), new("energyIntake", 2400), new("restingEnergyExpenditure", 1500),
            new("thermicEffectOfFood", 200), new("nonExerciseEnergyExpenditureNet", 300),
            new("exerciseEnergyExpenditureNet", 600), new("fatFreeMass", 60),
        ];
        var request = new ScienceFeedbackRequest(day, null, null, 1, values, null);
        (await user.PutAsJsonAsync(Endpoint, request with { Day = today }, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var report = await SaveAsync(user, request, ct);
        report.Nutrition.DiaryComplete.Should().BeTrue();
        report.Nutrition.TotalExpenditureKcal.Should().Be(2600);
        report.Nutrition.EnergyBalanceKcal.Should().Be(-200);
        report.Nutrition.EnergyAvailabilityKcalPerKg.Should().Be(30);
        var read = await user.GetFromJsonAsync<ScienceFeedbackReport>(
            Endpoint + "?day=" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ct);
        read!.Nutrition.Should().Be(report.Nutrition);
        var revised = await SaveAsync(user, request with { Revision = 2, Values = [new("energyIntake", 2400)] }, ct);
        revised.Nutrition.DiaryComplete.Should().BeFalse();
        revised.Nutrition.EnergyBalanceKcal.Should().BeNull();
        revised.Nutrition.EnergyAvailabilityKcalPerKg.Should().BeNull();
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
