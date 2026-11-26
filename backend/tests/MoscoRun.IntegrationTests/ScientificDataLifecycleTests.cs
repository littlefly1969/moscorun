using System.Text;
using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class ScientificDataLifecycleTests(ApiFactory factory)
{
    [Fact]
    public async Task Retention_preserves_transitive_lineage_and_is_idempotent()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var now = factory.Time.GetUtcNow();
        var oldTime = now.AddDays(-5);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ScientificObservationStore>();
        var lifecycle = scope.ServiceProvider.GetRequiredService<ScientificDataLifecycle>();
        var input = new ScientificObservation(Guid.NewGuid(), owner, "heartRate", "bpm", 140, oldTime, oldTime, "test", ObservationMethod.Measured);
        var middle = new ScientificObservation(Guid.NewGuid(), owner, "middle", "native", 1, oldTime, oldTime,
            "test", ObservationMethod.Derived, derivationVersion: "v1", inputIds: [input.Id]);
        var latest = new ScientificObservation(Guid.NewGuid(), owner, "latest", "native", 2, now, now,
            "test", ObservationMethod.Derived, derivationVersion: "v1", inputIds: [middle.Id]);
        var orphan = new ScientificObservation(Guid.NewGuid(), owner, "heartRate", "bpm", 150, oldTime, oldTime, "test", ObservationMethod.Measured);
        foreach (var item in new[] { input, middle, latest, orphan })
        {
            await store.AppendAsync(owner, item, ct);
        }

        var cutoff = now.AddDays(-1);
        var preview = await lifecycle.PreviewRetentionAsync(owner, cutoff, ct);
        preview.Observations.Should().Be(1);
        preview.ProtectedObservations.Should().Be(2);
        var operation = Guid.NewGuid();
        var applied = await lifecycle.ApplyRetentionAsync(owner, operation, cutoff, true, ct);
        applied.Should().Be(preview);
        (await lifecycle.ApplyRetentionAsync(owner, operation, cutoff, true, ct)).Should().Be(applied);
        (await store.ReadAsOfAsync(owner, now, ct)).Select(x => x.Id).Should().BeEquivalentTo(new[] { input.Id, middle.Id, latest.Id });
    }

    [Fact]
    public async Task Export_erase_fence_and_resume_are_owner_scoped()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var (_, other) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var time = factory.Time.GetUtcNow().AddDays(-1);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ScientificObservationStore>();
        var lifecycle = scope.ServiceProvider.GetRequiredService<ScientificDataLifecycle>();
        var first = new ScientificObservation(Guid.NewGuid(), owner, "heartRate", "bpm", 140, time, time, "test", ObservationMethod.Measured);
        var foreign = new ScientificObservation(Guid.NewGuid(), other, "heartRate", "bpm", 180, time, time, "test", ObservationMethod.Measured);
        await store.AppendAsync(owner, first, ct);
        await store.AppendAsync(other, foreign, ct);
        using var archive = new MemoryStream();
        (await lifecycle.ExportArchiveAsync(owner, Guid.NewGuid(), archive, ct)).Observations.Should().Be(1);
        var text = Encoding.UTF8.GetString(archive.ToArray());
        text.Should().Contain(first.Id.ToString()).And.NotContain(foreign.Id.ToString());
        var operation = Guid.NewGuid();
        var erased = await lifecycle.EraseAllAsync(owner, operation, true, ct);
        erased.Observations.Should().Be(1);
        (await lifecycle.EraseAllAsync(owner, operation, true, ct)).Should().Be(erased);
        (await store.ReadAsOfAsync(other, factory.Time.GetUtcNow(), ct)).Should().ContainSingle();
        Func<Task> blocked = () => store.AppendAsync(owner, first, ct);
        await blocked.Should().ThrowAsync<InvalidOperationException>();
        var audit = await lifecycle.ReadAuditAsync(owner, ct: ct);
        audit.Should().HaveCount(2);
        await lifecycle.ResumeIngestionAsync(owner, Guid.NewGuid(), true, ct);
        await store.AppendAsync(owner, first, ct);
        (await store.ReadAsOfAsync(owner, factory.Time.GetUtcNow(), ct)).Should().ContainSingle();
    }
}
