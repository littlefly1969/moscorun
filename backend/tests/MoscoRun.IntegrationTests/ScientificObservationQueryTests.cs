using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class ScientificObservationQueryTests(ApiFactory factory)
{
    [Fact]
    public async Task Pages_respect_owner_and_availability()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var (_, other) = await factory.CreateUserAsync();
        var time = new DateTimeOffset(2026, 11, 25, 8, 0, 0, TimeSpan.Zero);
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<ScientificObservationStore>();
        var first = new ScientificObservation(Guid.Parse("00000000-0000-4000-8000-000000000001"), owner, "heartRate", "bpm", 140, time, time, "test", ObservationMethod.Measured);
        var second = new ScientificObservation(Guid.Parse("00000000-0000-4000-8000-000000000002"), owner, "heartRate", "bpm", 150, time, time, "test", ObservationMethod.Measured);
        await writer.AppendAsync(owner, first, ct);
        await writer.AppendAsync(owner, second, ct);
        await writer.AppendAsync(owner, new ScientificObservation(Guid.NewGuid(), owner, "heartRate", "bpm", 160, time, time.AddHours(2), "test", ObservationMethod.Measured), ct);
        await writer.AppendAsync(other, new ScientificObservation(Guid.NewGuid(), other, "heartRate", "bpm", 200, time, time, "test", ObservationMethod.Measured), ct);
        var reader = scope.ServiceProvider.GetRequiredService<ScientificObservationQuery>();
        var page = await reader.ReadAsync(owner, time, time.AddHours(1), time.AddHours(1), "heartRate", limit: 1, ct: ct);
        page.Items.Should().ContainSingle().Which.Id.Should().Be(first.Id);
        page.NextCursor.Should().NotBeNull();
        var next = await reader.ReadAsync(owner, time, time.AddHours(1), time.AddHours(1), "heartRate", after: page.NextCursor, limit: 1, ct: ct);
        next.Items.Should().ContainSingle().Which.Id.Should().Be(second.Id);
        next.NextCursor.Should().BeNull();
    }
}
