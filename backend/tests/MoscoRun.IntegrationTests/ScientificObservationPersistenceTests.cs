using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class ScientificObservationPersistenceTests(ApiFactory factory)
{
    [Fact]
    public async Task Observations_survive_new_scope_and_preserve_owner_time_and_lineage()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var (_, otherOwner) = await factory.CreateUserAsync();
        var time = new DateTimeOffset(2026, 11, 25, 8, 0, 0, TimeSpan.Zero);
        var ct = TestContext.Current.CancellationToken;
        var input = new ScientificObservation(Guid.NewGuid(), owner, "heartRate", "bpm", 145, time, time.AddHours(1), "test", ObservationMethod.Measured);
        var derived = new ScientificObservation(Guid.NewGuid(), owner, "testDerived", "native", null, time, time.AddHours(2), "test", ObservationMethod.Derived, derivationVersion: "v1", inputIds: [input.Id]);
        var foreign = new ScientificObservation(Guid.NewGuid(), otherOwner, "heartRate", "bpm", 180, time, time, "test", ObservationMethod.Measured);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ScientificObservationStore>();
            await store.AppendAsync(owner, input, ct);
            await store.AppendAsync(owner, derived, ct);
            await store.AppendAsync(otherOwner, foreign, ct);
        }

        await using var readScope = factory.Services.CreateAsyncScope();
        var reader = readScope.ServiceProvider.GetRequiredService<ScientificObservationStore>();
        (await reader.ReadAsOfAsync(owner, time, ct)).Should().BeEmpty();
        (await reader.ReadAsOfAsync(owner, time.AddHours(1), ct)).Should().ContainSingle().Which.Id.Should().Be(input.Id);
        var result = await reader.ReadAsOfAsync(owner, time.AddHours(2), ct);
        result.Select(o => o.Id).Should().Equal(input.Id, derived.Id);
        result[1].InputIds.Should().Equal(input.Id);
        result[1].Value.Should().BeNull();
        result[1].DerivationVersion.Should().Be("v1");
        (await reader.ReadAsOfAsync(otherOwner, time.AddHours(2), ct)).Should().ContainSingle().Which.Id.Should().Be(foreign.Id);

        var invalid = new ScientificObservation(Guid.NewGuid(), owner, "testDerived", "native", 1, time, time.AddHours(2), "test", ObservationMethod.Derived, derivationVersion: "v1", inputIds: [foreign.Id]);
        Func<Task> append = () => reader.AppendAsync(owner, invalid, ct);
        await append.Should().ThrowAsync<ArgumentException>();
        Func<Task> wrongOwner = () => reader.AppendAsync(owner, foreign, ct);
        await wrongOwner.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Scientific_migration_matches_current_model()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>();
        db.Database.HasPendingModelChanges().Should().BeFalse();
    }
}
