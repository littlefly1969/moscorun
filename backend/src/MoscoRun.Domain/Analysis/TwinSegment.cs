namespace MoscoRun.Domain.Analysis;

/// <summary>Ingresso ipotetico: durata ed equilibrio cardiaco da modello calibrato, non da una FC futura osservata.</summary>
public sealed record TwinSegment(double DurationSeconds, double EquilibriumHeartRateBpm, double ExternalSpeedMetersPerSecond);
