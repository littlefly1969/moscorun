using System.Globalization;
using System.Net.Http.Json;
using MoscoRun.Api.Features.Workouts;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.IntegrationTests;

public sealed class SleepDiaryEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Uses_latest_coherent_diary_and_never_another_athletes_nights()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        using var user = client;
        var (other, _) = await factory.CreateUserClientAsync();
        using var stranger = other;
        var ct = TestContext.Current.CancellationToken;
        var last = DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime).AddDays(-1);
        for (var index = 0; index < 7; index++)
        {
            var request = new ScienceFeedbackRequest(last.AddDays(-index), null, null, 1,
                [new(SleepDiaryMetrics.Bedtime, 1430), new(SleepDiaryMetrics.Wake, 480),
                 new(SleepDiaryMetrics.InBed, 28800), new("sleepDuration", 25200), new(SleepDiaryMetrics.Confirmed, 1)], null);
            (await user.PutAsJsonAsync("/api/v1/science/feedback", request, ct)).EnsureSuccessStatusCode();
        }

        var uri = $"/api/v1/science/sleep?last={last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&days=7";
        var report = (await user.GetFromJsonAsync<SleepDiaryReport>(uri, ct))!;
        report.CompleteNights.Should().Be(7);
        report.MeanEfficiencyPercent.Should().Be(87.5);
        (await stranger.GetFromJsonAsync<SleepDiaryReport>(uri, ct))!.CompleteNights.Should().Be(0);
        var revoke = new ScienceFeedbackRequest(last, null, null, 2, [new(SleepDiaryMetrics.Confirmed, 0)], null);
        (await user.PutAsJsonAsync("/api/v1/science/feedback", revoke, ct)).EnsureSuccessStatusCode();
        (await user.GetFromJsonAsync<SleepDiaryReport>(uri, ct))!.Bedtime.Should().BeNull();
    }
}
