using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed record ExtendedBiomechanicsReport(Guid WorkoutId, long WorkoutRevision, string WorkoutContentSha256,
    string Source, DateTimeOffset AsOf, string MethodVersion, string Status, double DurationSeconds,
    bool Truncated, bool ProtocolConfirmed, bool CadenceIsStepsPerMinute, bool BalanceIsLeftPercent,
    IReadOnlyList<BiomechanicsChunkMetadata> Chunks, IReadOnlyList<BiomechanicsChunkMetadata> Selected,
    IReadOnlyList<BiomechanicsMetricSummary> Metrics);
