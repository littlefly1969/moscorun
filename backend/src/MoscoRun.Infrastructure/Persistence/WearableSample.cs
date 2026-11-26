using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Campione normalizzato; la provenienza conserva il dato nativo se disponibile.</summary>
public sealed record WearableSample(string ExternalId, string Revision, string Metric, string Unit,
    double? Value, DateTimeOffset Time, ObservationMethod Method, string? ProviderAlgorithm = null,
    long? RevisionOrder = null, ScientificImportProvenance? Provenance = null);
