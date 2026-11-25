namespace MoscoRun.Domain.Analysis;

public sealed record CriticalSpeedFit(
    double CriticalSpeedMetersPerSecond,
    double DistanceReserveMeters,
    double RootMeanSquaredErrorMeters,
    int EffortCount);
