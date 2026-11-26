namespace MoscoRun.Domain.Analysis;

/// <summary>Una media di sessione su finestre accoppiate a condizioni note, per una sola metrica e unità.</summary>
public sealed record BiomechanicalSession(
    Guid OwnerId, Guid SessionId, DateTimeOffset Time, DateTimeOffset AvailableAt,
    string Source, string Metric, string Unit, double SpeedMetersPerSecond, double GradePercent, double Value);
