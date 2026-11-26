using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Campione già normalizzato dal connettore; l'ID deve identificare il singolo campione.</summary>
public sealed record WearableSample(string ExternalId, string Revision, string Metric, string Unit,
    double? Value, DateTimeOffset Time, ObservationMethod Method, string? ProviderAlgorithm = null);

public sealed class WearableImport(ScientificImportStore store)
{
    public Task<int> ImportAsync(Guid ownerId, string source, IReadOnlyList<WearableSample> samples,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (source is not ("garmin-connect" or "samsung-health" or "health-connect"))
        {
            throw new ArgumentException("Sorgente wearable non supportata.", nameof(source));
        }

        if (samples.Count > ScientificImportStore.MaxBatchSize ||
            samples.Any(s => s.Method is not (ObservationMethod.Measured or ObservationMethod.ProviderReported or ObservationMethod.ProviderEstimated)))
        {
            throw new ArgumentException("Batch o metodo wearable non valido.", nameof(samples));
        }

        var items = samples.Select(s => new ScientificImportItem(source, s.ExternalId, s.Revision, s.Metric,
            s.Unit, s.Value, s.Time, s.Method, s.ProviderAlgorithm)).ToArray();
        return store.ImportAsync(ownerId, items, ct);
    }
}
