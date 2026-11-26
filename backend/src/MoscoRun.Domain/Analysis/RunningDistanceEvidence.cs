namespace MoscoRun.Domain.Analysis;

public sealed record RunningDistanceEvidence(Guid WorkoutId, long Revision, string ContentSha256, string Source,
    string ActivityType, DateTimeOffset StartedAt, DateTimeOffset EndedAt, DateTimeOffset AvailableAt, double? DistanceMeters);
