using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class ScientificImportTests(ApiFactory factory)
{
    [Fact]
    public async Task Replay_and_revision_preserve_history()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var time = factory.Time.GetUtcNow().AddDays(-1);
        var item = new ScientificImportItem("garmin-connect", "sample-1", "1", "heartRate", "bpm", 145, time, ObservationMethod.ProviderReported);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = new ScientificImportStore(scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>(), factory.Time);
        (await store.ImportAsync(owner, [item], ct)).Should().Be(1);
        (await store.ImportAsync(owner, [item], ct)).Should().Be(0);
        Func<Task> conflict = () => store.ImportAsync(owner, [item with { Value = 146 }], ct);
        await conflict.Should().ThrowAsync<InvalidOperationException>();
        (await store.ImportAsync(owner, [item with { Revision = "2", Value = 146 }], ct)).Should().Be(1);
        var reader = scope.ServiceProvider.GetRequiredService<ScientificObservationStore>();
        var values = await reader.ReadAsOfAsync(owner, factory.Time.GetUtcNow(), ct);
        values.Select(o => o.Value).Should().BeEquivalentTo(new double?[] { 145, 146 });
    }
}
