using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class RrChunkEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Owner_can_select_immutable_rr_revision_but_not_another_athletes_chunk()
    {
        var (client, owner) = await factory.CreateUserClientAsync();
        using var user = client;
        var (other, _) = await factory.CreateUserClientAsync();
        using var stranger = other;
        var ct = TestContext.Current.CancellationToken;
        var time = factory.Time.GetUtcNow().AddDays(-2);
        var day = DateOnly.FromDateTime(time.UtcDateTime);
        var samples = Enumerable.Range(0, 200).Select(index =>
            new ScientificSignalSample(time.AddMilliseconds(index * 800.0), 800)).ToArray();
        var chunk = new ScientificSignalChunk("garmin-test", "rr-segment", "r1", "rrInterval", "ms",
            ObservationMethod.Measured, samples, RevisionOrder: 1);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ScientificSignalStore>();
        await store.ImportAsync(owner, chunk, ct);
        await store.ImportAsync(owner, chunk with { Revision = "r2", RevisionOrder = 2 }, ct);
        var uri = $"/api/v1/science/rr?source=garmin-test&day={day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&context=resting&screened=true&timestampsAreBeatEnds=true";
        var report = (await user.GetFromJsonAsync<RrChunkReport>(uri, ct))!;
        report.Chunks.Should().HaveCount(2);
        report.Analysis.Should().BeNull();
        var selected = report.Chunks.Single(item => string.Equals(item.Revision, "r1", StringComparison.Ordinal));
        var chosen = (await user.GetFromJsonAsync<RrChunkReport>(uri + "&chunkId=" + selected.Id, ct))!;
        chosen.Selected!.Revision.Should().Be("r1");
        chosen.Analysis!.Hrv!.RmssdMilliseconds.Should().Be(0);
        chosen.Analysis.Dfa.Should().BeNull();
        (await stranger.GetFromJsonAsync<RrChunkReport>(uri, ct))!.Chunks.Should().BeEmpty();
        (await stranger.GetAsync(uri + "&chunkId=" + selected.Id, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
