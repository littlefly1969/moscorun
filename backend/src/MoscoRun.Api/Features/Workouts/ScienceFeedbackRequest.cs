namespace MoscoRun.Api.Features.Workouts;

public sealed record ScienceFeedbackRequest(
    DateOnly? Day, Guid? WorkoutId, long? WorkoutRevision, long Revision,
    IReadOnlyList<ScienceFeedbackInput> Values, string? Note);
