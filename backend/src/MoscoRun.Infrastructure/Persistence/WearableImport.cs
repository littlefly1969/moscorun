using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Confine di persistenza dei connettori: il mapping nativo resta nel livello acquisizione.</summary>
public sealed class WearableImport(ScientificImportStore store, ScientificSyncStore sync)
{
    public Task<int> ImportAsync(Guid ownerId, string source, IReadOnlyList<WearableSample> samples,
        CancellationToken ct = default) => store.ImportAsync(ownerId, Convert(source, samples), ct);

    public Task<int> ImportPageAsync(Guid ownerId, string source, string stream, string? previousCursor,
        string nextCursor, IReadOnlyList<WearableSample> samples, CancellationToken ct = default) =>
        sync.ImportPageAsync(ownerId, new ScientificSyncPage(source, stream, previousCursor, nextCursor,
            Convert(source, samples)), ct);

    public Task<string?> ReadCursorAsync(Guid ownerId, string source, string stream, CancellationToken ct = default)
    {
        ValidateSource(source);
        return sync.ReadCursorAsync(ownerId, source, stream, ct);
    }

    private static ScientificImportItem[] Convert(string source, IReadOnlyList<WearableSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ValidateSource(source);
        if (samples.Count > ScientificImportStore.MaxBatchSize ||
            samples.Any(s => !WearableIngestCatalog.AcceptsMethod(s.Method)))
        {
            throw new ArgumentException("Batch o metodo wearable non valido.", nameof(samples));
        }

        return samples.Select(s => new ScientificImportItem(source, s.ExternalId, s.Revision, s.Metric,
            s.Unit, s.Value, s.Time, s.Method, s.ProviderAlgorithm, s.RevisionOrder, s.Provenance)).ToArray();
    }

    private static void ValidateSource(string source)
    {
        if (!WearableIngestCatalog.IsSource(source))
        {
            throw new ArgumentException("Sorgente wearable non supportata.", nameof(source));
        }
    }
}
