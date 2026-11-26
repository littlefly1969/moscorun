using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class ScientificPredictionTests(ApiFactory factory)
{
    [Fact]
    public async Task Model_output_preserves_version_and_inputs()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var now = factory.Time.GetUtcNow();
        var time = now.AddDays(-1);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ScientificObservationStore>();
        var input = new ScientificObservation(Guid.NewGuid(), owner, "heartRate", "bpm", 145, time, time, "test", ObservationMethod.Measured);
        await store.AppendAsync(owner, input, ct);
        var predictions = new ScientificPredictionStore(store, factory.Time);
        var id = Guid.NewGuid();
        await predictions.AppendAsync(owner, id, "testPrediction", "native", 42, time, "twin", "v1", [input.Id], ct);
        (await store.ReadAsOfAsync(owner, time, ct)).Should().ContainSingle();
        var result = await store.ReadAsOfAsync(owner, now, ct);
        var output = result.Single(o => o.Id == id);
        output.Method.Should().Be(ObservationMethod.ModelInferred);
        output.DerivationVersion.Should().Be("v1");
        output.InputIds.Should().Equal(input.Id);
    }
}
