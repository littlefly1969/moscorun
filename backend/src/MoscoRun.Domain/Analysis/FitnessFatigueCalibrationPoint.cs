namespace MoscoRun.Domain.Analysis;

/// <summary>Endpoint omogeneo osservato; l'esito deve precedere la disponibilità.</summary>
public sealed record FitnessFatigueCalibrationPoint(
    Guid OwnerId, Guid ObservationId, DateTimeOffset Time, DateTimeOffset AvailableAt, double Response);
