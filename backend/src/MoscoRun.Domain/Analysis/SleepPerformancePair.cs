namespace MoscoRun.Domain.Analysis;

public sealed record SleepPerformancePair(Guid WorkoutId, long WorkoutRevision, string ContentSha256, long FeedbackRevision,
    DateTimeOffset TestStartedAt, DateTimeOffset TestEndedAt, DateTimeOffset TestAvailableAt, double SpeedMetersPerSecond,
    DateOnly SleepDay, long? SleepRevision, DateTimeOffset? SleepAvailableAt, string Status,
    double? SleepHours, double? SleepEfficiencyPercent);
