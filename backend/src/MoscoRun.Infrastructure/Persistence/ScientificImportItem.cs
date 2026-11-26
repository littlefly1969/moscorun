using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificImportItem(string Source, string ExternalId, string Revision,
    string Metric, string Unit, double? Value, DateTimeOffset Time, ObservationMethod Method, string? ProviderAlgorithm = null);
