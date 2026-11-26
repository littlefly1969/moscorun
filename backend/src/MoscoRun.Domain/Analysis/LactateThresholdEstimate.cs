namespace MoscoRun.Domain.Analysis;

public sealed record LactateThresholdEstimate(double SpeedMetersPerSecond, double HeartRateBpm, double LactateMillimolesPerLiter);
