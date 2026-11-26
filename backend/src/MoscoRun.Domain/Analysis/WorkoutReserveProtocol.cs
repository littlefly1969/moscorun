using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutReserveProtocol(SeriesMetric Metric, double CriticalIntensity, double Capacity,
    double InitialRemaining, double RecoveryTimeConstantSeconds)
{
    public bool IsValid() => Metric is SeriesMetric.Speed or SeriesMetric.Power &&
        Positive(CriticalIntensity) && Positive(Capacity) && double.IsFinite(InitialRemaining) &&
        InitialRemaining > 0 && InitialRemaining <= Capacity && Positive(RecoveryTimeConstantSeconds);

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
