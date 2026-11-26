using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutDurabilityProtocol(
    SeriesMetric Metric, int StartOffsetSeconds, int WindowSeconds, int WindowCount,
    double MinimumCoverage, double OnsetLossPercent, int MinimumConsecutiveWindows)
{
    public bool IsValid() =>
        Metric is SeriesMetric.Speed or SeriesMetric.Power &&
        StartOffsetSeconds >= 0 && StartOffsetSeconds <= Workout.MaxDuration.TotalSeconds &&
        WindowSeconds is >= 30 and <= 3600 && WindowCount is >= 3 and <= 30 &&
        double.IsFinite(MinimumCoverage) && MinimumCoverage > 0 && MinimumCoverage <= 1 &&
        double.IsFinite(OnsetLossPercent) && OnsetLossPercent > 0 && OnsetLossPercent <= 100 &&
        MinimumConsecutiveWindows >= 1 && MinimumConsecutiveWindows < WindowCount;

    public bool Fits(TimeSpan duration) =>
        IsValid() && StartOffsetSeconds + ((double)WindowSeconds * WindowCount) <= duration.TotalSeconds;
}
