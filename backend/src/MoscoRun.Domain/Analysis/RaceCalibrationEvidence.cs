namespace MoscoRun.Domain.Analysis;

public sealed record RaceCalibrationEvidence(Guid WorkoutId, long Revision, string ContentSha256,
    DateTimeOffset EndedAt, DateTimeOffset AvailableAt, long FeedbackRevision, double DistanceMeters,
    double DurationSeconds, string Role, double? PredictedSeconds, double? AbsoluteErrorSeconds, double? MeanPowerWatts);
