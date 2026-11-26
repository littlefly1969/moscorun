using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class ScientificPredictionTests(ApiFactory factory)
{
    [Fact]
    public async Task Registry_run_and_digital_twin_preserve_lineage_and_versions()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var (_, other) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var now = factory.Time.GetUtcNow();
        var time = now.AddDays(-1);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ScientificObservationStore>();
        var registry = scope.ServiceProvider.GetRequiredService<ScientificModelRegistry>();
        var runs = scope.ServiceProvider.GetRequiredService<ScientificModelRunStore>();
        var artifact = new ScientificModelArtifact("twin", "v1", "json", [1, 2, 3], "features-v1", time, "{}", "{}");
        (await registry.RegisterAsync(owner, artifact, ct)).Should().BeTrue();
        (await registry.RegisterAsync(owner, artifact, ct)).Should().BeFalse();
        (await registry.ReadAsync(other, artifact.ModelId, artifact.Version, now, ct)).Should().BeNull();
        (await registry.ReadAsync(owner, artifact.ModelId, artifact.Version, now, ct))!.Definition.Artifact.Should().Equal(artifact.Artifact);

        var input = new ScientificObservation(Guid.NewGuid(), owner, "heartRate", "bpm", 145, time, time, "test", ObservationMethod.Measured);
        await store.AppendAsync(owner, input, ct);
        var output = new ScientificModelOutput(Guid.NewGuid(), "testPrediction", "native", 42, 40, 45);
        var run = new ScientificModelRun(Guid.NewGuid(), "twin", "v1", ScientificModelRunMode.Operational,
            now, now.AddDays(1), [input.Id], [output], "{\"fitness\":42}");
        (await runs.SaveAsync(owner, run, ct)).Should().BeTrue();
        (await runs.SaveAsync(owner, run, ct)).Should().BeFalse();
        (await runs.ReadAsync(owner, run.Id, time, ct)).Should().BeNull();
        (await runs.ReadAsync(other, run.Id, now, ct)).Should().BeNull();
        var latest = await runs.ReadLatestStateAsync(owner, "twin", "v1", now, ct);
        latest!.Run.Outputs.Should().ContainSingle().Which.Should().Be(output);
        var values = await store.ReadAsOfAsync(owner, now, ct);
        var predicted = values.Single(o => o.Id == output.Id);
        predicted.Method.Should().Be(ObservationMethod.ModelInferred);
        predicted.InputIds.Should().Equal(input.Id);
        predicted.DerivationVersion.Should().Be("v1");
    }

    [Fact]
    public async Task Backtest_cannot_use_inputs_learned_after_the_decision()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var now = factory.Time.GetUtcNow();
        var decision = now.AddDays(-1);
        await using var scope = factory.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<ScientificModelRegistry>();
        var store = scope.ServiceProvider.GetRequiredService<ScientificObservationStore>();
        var runs = scope.ServiceProvider.GetRequiredService<ScientificModelRunStore>();
        await registry.RegisterAsync(owner, new ScientificModelArtifact("model", "1", "json", [1], "f1",
            decision.AddDays(-1), "{}", "{}"), ct);
        var late = new ScientificObservation(Guid.NewGuid(), owner, "heartRate", "bpm", 140, decision, now, "test", ObservationMethod.Measured);
        await store.AppendAsync(owner, late, ct);
        var run = new ScientificModelRun(Guid.NewGuid(), "model", "1", ScientificModelRunMode.Backtest,
            decision, null, [late.Id], [new ScientificModelOutput(Guid.NewGuid(), "prediction", "native", 1)]);
        Func<Task> save = () => runs.SaveAsync(owner, run, ct);
        await save.Should().ThrowAsync<InvalidOperationException>();
        (await runs.ReadAsync(owner, run.Id, now, ct)).Should().BeNull();
    }
}
