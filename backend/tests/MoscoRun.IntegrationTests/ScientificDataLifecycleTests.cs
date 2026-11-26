using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class ScientificDataLifecycleTests(ApiFactory factory)
{
    [Fact]
    public async Task Export_and_erase_are_owner_scoped()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var (_, other) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var time = factory.Time.GetUtcNow().AddDays(-1);
        await using var scope = factory.Services.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<ScientificObservationStore>();
        await writer.AppendAsync(owner, new ScientificObservation(Guid.NewGuid(), owner, "heartRate", "bpm", 140, time, time, "test", ObservationMethod.Measured), ct);
        await writer.AppendAsync(other, new ScientificObservation(Guid.NewGuid(), other, "heartRate", "bpm", 180, time, time, "test", ObservationMethod.Measured), ct);
        var lifecycle = new ScientificDataLifecycle(scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>());
        (await lifecycle.CountBeforeAsync(owner, time.AddDays(1), ct)).Should().Be(1);
        (await lifecycle.ExportAsync(owner, ct)).Should().ContainSingle().Which.OwnerId.Should().Be(owner);
        (await lifecycle.EraseAllAsync(owner, ct)).Should().Be(1);
        (await lifecycle.ExportAsync(owner, ct)).Should().BeEmpty();
        (await lifecycle.ExportAsync(other, ct)).Should().ContainSingle();
    }
}
