namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScienceDiagnosticReport(string Source, Guid RunId, DateTimeOffset StartedAt, DateTimeOffset FinishedAt,
    string State, DateTimeOffset? FirstReadFrom, DateTimeOffset? ReadThrough, int WindowsRead,
    IReadOnlyList<ScienceMetricDiagnostic> Metrics);
