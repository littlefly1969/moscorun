using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class ScientificSyncTests(ApiFactory factory)
{
    [Fact]
    public async Task Checkpoint_and_revisions_are_committed_atomically()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var (_, other) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var time = factory.Time.GetUtcNow().AddDays(-1);
        var item = new ScientificImportItem("garmin-connect", "sample", "r1", "heartRate", "bpm", 140, time,
            ObservationMethod.ProviderReported, RevisionOrder: 1);
        await using var scope = factory.Services.CreateAsyncScope();
        var sync = scope.ServiceProvider.GetRequiredService<ScientificSyncStore>();
        var page = new ScientificSyncPage(item.Source, "samples", null, "c1", [item]);
        (await sync.ImportPageAsync(owner, page, ct)).Should().Be(1);
        (await sync.ImportPageAsync(owner, page, ct)).Should().Be(0);
        (await sync.ReadCursorAsync(owner, item.Source, "samples", ct)).Should().Be("c1");
        (await sync.ReadCursorAsync(other, item.Source, "samples", ct)).Should().BeNull();

        var revision = item with { Revision = "r2", RevisionOrder = 2, Value = 150 };
        Func<Task> badPage = () => sync.ImportPageAsync(owner, page with { Items = [revision] }, ct);
        await badPage.Should().ThrowAsync<InvalidOperationException>();
        var query = scope.ServiceProvider.GetRequiredService<ScientificRevisionQuery>();
        (await query.ReadHistoryAsync(owner, item.Source, item.ExternalId, item.Metric, factory.Time.GetUtcNow(), ct))
            .Should().ContainSingle();
        (await sync.ImportPageAsync(owner, page with { PreviousCursor = "c1", NextCursor = "c2", Items = [revision] }, ct))
            .Should().Be(1);
        var latest = await query.ReadLatestOrderedAsync(owner, item.Source, item.ExternalId, item.Metric, factory.Time.GetUtcNow(), ct);
        latest!.Observation.Value.Should().Be(150);
        (await sync.ReadCursorAsync(owner, item.Source, "samples", ct)).Should().Be("c2");
    }
}
