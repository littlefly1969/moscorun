namespace MoscoRun.Domain.Analysis;

public sealed record SessionLoadEvidence(
    Guid WorkoutId, long Revision, DateTimeOffset StartedAt, DateTimeOffset EndedAt, DateTimeOffset UpdatedAt,
    double? ActiveDurationSeconds, double? Rpe, DateTimeOffset? RpeAvailableAt, bool FeedbackAmbiguous);
