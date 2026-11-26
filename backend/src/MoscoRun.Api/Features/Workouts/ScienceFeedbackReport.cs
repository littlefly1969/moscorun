using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

public sealed record ScienceFeedbackReport(
    DateOnly? Day, Guid? WorkoutId, long? WorkoutRevision, long? Revision, DateTimeOffset? AvailableAt,
    IReadOnlyList<AthleteFeedbackValue> Values, string? Note, string Status,
    double? ActiveDurationSeconds, double? SessionRpeLoad, string SessionLoadMethodVersion);
