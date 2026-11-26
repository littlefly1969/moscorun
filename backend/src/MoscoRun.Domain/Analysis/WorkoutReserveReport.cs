namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutReserveReport(Guid WorkoutId, long Revision, string ContentSha256, string Source,
    DateTimeOffset AsOf, string MethodVersion, WorkoutReserveProtocol Protocol, string Unit, string Status,
    double WorkoutDurationSeconds, double SupportedSeconds, double EvaluatedSeconds, int EvaluatedSegments,
    double? MinimumRemaining, double? LastRemaining, double? ExhaustionSeconds, IReadOnlyList<ReserveState> Points);
