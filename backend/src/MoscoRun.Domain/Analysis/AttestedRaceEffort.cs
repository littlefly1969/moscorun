namespace MoscoRun.Domain.Analysis;

public sealed record AttestedRaceEffort(Guid OwnerId, Guid WorkoutId, long Revision, string ContentSha256,
    string Source, DateTimeOffset StartedAt, DateTimeOffset EndedAt, DateTimeOffset AvailableAt,
    long FeedbackRevision, double DistanceMeters, double? MeanPowerWatts = null);
