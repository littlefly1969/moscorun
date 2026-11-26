namespace MoscoRun.Infrastructure.Persistence;

internal sealed class AthleteFeedbackEntryRow
{
    public Guid OwnerId { get; init; }

    public Guid Id { get; init; }

    public string EntryId { get; init; } = string.Empty;

    public string Revision { get; init; } = string.Empty;

    public long? RevisionOrder { get; init; }

    public DateTimeOffset Time { get; init; }

    public DateTimeOffset AvailableAt { get; init; }

    public Guid? WorkoutId { get; init; }

    public string? Note { get; init; }

    public Guid[] ObservationIds { get; init; } = [];
}
