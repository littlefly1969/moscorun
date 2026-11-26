namespace MoscoRun.Domain.Analysis;

public sealed record AdaptationTestEvidence(Guid WorkoutId, long Revision, string ContentSha256, long FeedbackRevision,
    DateTimeOffset StartedAt, DateTimeOffset EndedAt, DateTimeOffset AvailableAt, string Role,
    double ObservedSpeedMetersPerSecond, double? PredictedSpeedMetersPerSecond, double? BaselineSpeedMetersPerSecond);
