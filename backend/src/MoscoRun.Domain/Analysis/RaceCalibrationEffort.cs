namespace MoscoRun.Domain.Analysis;

/// <summary>Prestazione massimale attestata, con protocollo e disponibilità osservabile.</summary>
public sealed record RaceCalibrationEffort(
    Guid OwnerId, Guid SessionId, string Protocol, DateTimeOffset Time, DateTimeOffset AvailableAt,
    double DistanceMeters, double DurationSeconds, bool IsMaximal);
