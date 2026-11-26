using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificSignalChunk(string Source, string ExternalId, string Revision, string Metric,
    string Unit, ObservationMethod Method, IReadOnlyList<ScientificSignalSample> Samples,
    string? ProviderAlgorithm = null, long? RevisionOrder = null, Guid? DeviceId = null, Guid? WorkoutId = null);
