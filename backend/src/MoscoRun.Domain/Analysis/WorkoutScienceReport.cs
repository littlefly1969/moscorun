namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutScienceReport(
    Guid WorkoutId, long Revision, string ContentSha256, string Source, DateTimeOffset AsOf,
    string MethodVersion, double MaxGapSeconds, IReadOnlyList<WorkoutChannelReport> Channels);
