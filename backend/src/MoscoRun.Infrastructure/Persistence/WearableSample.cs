using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed record WearableSample(string ExternalId, string Revision, string Metric, string Unit,
    double? Value, DateTimeOffset Time, ObservationMethod Method, string? ProviderAlgorithm = null);
