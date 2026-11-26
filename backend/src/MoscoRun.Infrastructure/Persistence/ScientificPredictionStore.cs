namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Convenienza per un singolo output operativo di una versione già registrata.</summary>
public sealed class ScientificPredictionStore(ScientificModelRunStore runs)
{
    public async Task AppendAsync(Guid ownerId, Guid predictionId, string metric, string unit, double? value,
        DateTimeOffset referenceTime, string modelId, string modelVersion, IReadOnlyList<Guid> inputs,
        CancellationToken ct = default)
    {
        _ = await runs.SaveAsync(ownerId, new ScientificModelRun(predictionId, modelId, modelVersion,
            ScientificModelRunMode.Operational, referenceTime, null, inputs,
            [new ScientificModelOutput(predictionId, metric, unit, value)]), ct);
    }
}
