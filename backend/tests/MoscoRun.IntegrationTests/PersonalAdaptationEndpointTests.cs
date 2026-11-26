using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.IntegrationTests;

public sealed class PersonalAdaptationEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Missing_history_is_not_a_zero_load_or_a_fabricated_model()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        using var user = client;
        var ct = TestContext.Current.CancellationToken;
        var today = DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime);
        var first = today.AddDays(-60);
        var split = today.AddDays(-20);
        var uri = $"/api/v1/science/adaptation?source=samsung-health&from={Date(first)}&split={Date(split)}&until={Date(today)}&distanceMeters=5000";
        var report = (await user.GetFromJsonAsync<PersonalAdaptationReport>(uri, ct))!;
        report.Loads.Should().HaveCount(60);
        report.Loads.Should().OnlyContain(point => point.Load == null);
        report.Model.Should().BeNull();
        report.CurrentPredictedSpeedMetersPerSecond.Should().BeNull();
        report.Tests.Should().BeEmpty();
        (await user.GetAsync(uri.Replace("distanceMeters=5000", "distanceMeters=0", StringComparison.Ordinal), ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string Date(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
