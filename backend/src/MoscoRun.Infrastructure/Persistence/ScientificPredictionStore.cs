using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Output scalari versionati; non esegue né addestra il modello.</summary>
public sealed class ScientificPredictionStore(ScientificObservationStore observations, TimeProvider clock)
{
    public Task AppendAsync(Guid ownerId, Guid predictionId, string metric, string unit, double? value,
        DateTimeOffset referenceTime, string modelId, string modelVersion, IReadOnlyList<Guid> inputs,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelVersion);
        ArgumentNullException.ThrowIfNull(inputs);
        var output = new ScientificObservation(predictionId, ownerId, metric, unit, value, referenceTime,
            clock.GetUtcNow(), modelId, ObservationMethod.ModelInferred, derivationVersion: modelVersion, inputIds: inputs);
        return observations.AppendAsync(ownerId, output, ct);
    }
}
