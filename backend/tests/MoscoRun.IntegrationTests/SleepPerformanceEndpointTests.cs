using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.IntegrationTests;

public sealed class SleepPerformanceEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Empty_history_returns_no_association_and_invalid_lag_is_refused()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        using var user = client;
        var ct = TestContext.Current.CancellationToken;
        var last = DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime).AddDays(-1);
        var uri = $"/api/v1/science/sleep-performance?source=samsung-health&last={last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&days=28&lag=1&distanceMeters=5000";
        var report = (await user.GetFromJsonAsync<SleepPerformanceReport>(uri, ct))!;
        report.PairedTests.Should().Be(0);
        report.SleepHoursCorrelation.Should().BeNull();
        report.SleepEfficiencyCorrelation.Should().BeNull();
        (await user.GetAsync(uri.Replace("&lag=1", "&lag=0", StringComparison.Ordinal), ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
