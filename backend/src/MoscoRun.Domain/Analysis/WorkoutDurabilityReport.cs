namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutDurabilityReport(
    Guid WorkoutId, long Revision, string ContentSha256, string Source, DateTimeOffset AsOf,
    string MethodVersion, string WindowMethodVersion, double MaxGapSeconds, WorkoutDurabilityProtocol Protocol,
    string Status, IReadOnlyList<WorkoutDurabilityWindow> Windows, DurabilityProfile? Profile);
