namespace MoscoRun.Domain.Analysis;

/// <summary>Moltiplicatore relativo di costo/tempo noto prima della simulazione; non inferito dal dislivello da solo.</summary>
public sealed record PacingSegment(double DistanceMeters, double RelativeTimeCost);
