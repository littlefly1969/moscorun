namespace MoscoRun.Domain.Analysis;

public sealed record CooperTestResult(Guid WorkoutId, long Revision, DateTimeOffset EndedAt,
    double DistanceMeters, double DurationSeconds, double EstimatedVo2MaxMlKgMin);
