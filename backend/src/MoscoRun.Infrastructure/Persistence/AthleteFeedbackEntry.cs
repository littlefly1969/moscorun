namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Una revisione immutabile di feedback, eventualmente associata a un allenamento.</summary>
public sealed record AthleteFeedbackEntry(string EntryId, string Revision, DateTimeOffset Time,
    IReadOnlyList<AthleteFeedbackValue> Values, Guid? WorkoutId = null, long? RevisionOrder = null, string? Note = null);
