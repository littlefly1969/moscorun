namespace MoscoRun.Domain.Analysis;

/// <summary>Velocità in m/s per D' oppure potenza in W per W'; non mescolare le due rappresentazioni.</summary>
public sealed record ReserveSegment(double DurationSeconds, double Intensity, double RecoveryTimeConstantSeconds);
