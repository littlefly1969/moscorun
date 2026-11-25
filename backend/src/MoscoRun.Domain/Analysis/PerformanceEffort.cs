namespace MoscoRun.Domain.Analysis;

/// <summary>Sforzo candidato, in secondi e metri. IsMaximal deve essere attestato, non inferito dal miglior segmento.</summary>
public readonly record struct PerformanceEffort(double DurationSeconds, double DistanceMeters, bool IsMaximal);
