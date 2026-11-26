using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class DailyScienceTests(ApiFactory factory)
{
    [Fact]
    public async Task Endpoint_reads_real_postgres_history_and_never_exposes_other_owner()
    {
        var (client, owner) = await factory.CreateUserClientAsync();
        using var ownedClient = client;
        var (foreign, _) = await factory.CreateUserClientAsync();
        using var foreignClient = foreign;
        var ct = TestContext.Current.CancellationToken;
        var day = DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime).AddDays(-2);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ScientificObservationStore>();
        for (var i = 0; i < 8; i++)
        {
            var t = At(day.AddDays(i - 7));
            await store.AppendAsync(owner, new ScientificObservation(Guid.NewGuid(), owner, "weight",
                "kg", 80 - i * 0.1, t, t, "scale-test", ObservationMethod.Measured), ct);
        }

        var uri = UriFor(day, "scale-test");
        var response = await ownedClient.GetAsync(uri, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var report = await response.Content.ReadFromJsonAsync<DailyMetricReport>(ct);
        report!.Baseline!.BaselineCount.Should().Be(7);
        report.Current.Value.Should().BeApproximately(79.3, 1e-9);
        report.WeightTrendKgPerWeek.Should().BeApproximately(-0.7, 1e-9);
        var isolated = await foreignClient.GetFromJsonAsync<DailyMetricReport>(uri, ct);
        isolated!.Status.Should().Be("missing");
        isolated.History.Should().OnlyContain(point => point.Value == null && point.ObservationId == null);
        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync(uri, ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ordered_revisions_resolve_but_unordered_revisions_refuse_and_cutoff_is_enforced()
    {
        var (client, owner) = await factory.CreateUserClientAsync();
        using var ownedClient = client;
        var ct = TestContext.Current.CancellationToken;
        var now = factory.Time.GetUtcNow();
        var day = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-2);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ScientificImportStore>();
        var first = new ScientificImportItem("garmin-test", "day", "r1", "weight", "kg", 80,
            At(day), ObservationMethod.ProviderReported, RevisionOrder: 1);
        await store.ImportAsync(owner, [first, first with { Revision = "r2", RevisionOrder = 2, Value = 79 }], ct);
        var uri = UriFor(day, "garmin-test");
        var selected = await ownedClient.GetFromJsonAsync<DailyMetricReport>(uri, ct);
        selected!.Current.Value.Should().Be(79);
        var historical = await ownedClient.GetFromJsonAsync<DailyMetricReport>(
            uri + "&asOf=" + Uri.EscapeDataString(now.AddHours(-1).ToString("O", CultureInfo.InvariantCulture)), ct);
        historical!.Status.Should().Be("missing");
        await store.ImportAsync(owner, [first with { Revision = "unknown", RevisionOrder = null, Value = 90 }], ct);
        var ambiguous = await ownedClient.GetFromJsonAsync<DailyMetricReport>(uri, ct);
        ambiguous!.Status.Should().Be("ambiguous");
        ambiguous.Current.Value.Should().BeNull();
        (await ownedClient.GetAsync(UriFor(day.AddDays(2), "garmin-test"), ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static DateTimeOffset At(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static string UriFor(DateOnly day, string source) =>
        $"/api/v1/science/daily?metric=weight&source={source}&day={day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&baselineDays=7";
}
