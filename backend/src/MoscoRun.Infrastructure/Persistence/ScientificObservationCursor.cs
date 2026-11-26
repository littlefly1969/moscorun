namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Posizione keyset; riutilizzare gli stessi filtri e lo stesso cutoff.</summary>
public sealed record ScientificObservationCursor(DateTimeOffset Time, DateTimeOffset AvailableAt, Guid Id);
