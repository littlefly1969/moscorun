namespace MoscoRun.Domain.Analysis;

/// <summary>Baseline descrittiva distanza-tempo. Non produce una misura diretta del VO2max o delle soglie.</summary>
public static class PerformancePhysiology
{
    public const string MethodVersion = "distance-time-ols-v2";

    public const double MinimumDurationSeconds = 120;
    public const double MaximumDurationSeconds = 1200;
    public const double MinimumDurationRatio = 2;
    public const double DurationToleranceSeconds = 0.001;

    public static CriticalSpeedFit? FitCriticalSpeed(IEnumerable<PerformanceEffort> efforts)
    {
        ArgumentNullException.ThrowIfNull(efforts);
        var eligible = efforts.Where(effort => effort.IsMaximal &&
            double.IsFinite(effort.DurationSeconds) && double.IsFinite(effort.DistanceMeters) &&
            effort.DurationSeconds >= MinimumDurationSeconds && effort.DurationSeconds <= MaximumDurationSeconds && effort.DistanceMeters > 0)
            .GroupBy(effort => Math.Round(effort.DurationSeconds, 3))
            .Select(group => group.MaxBy(effort => effort.DistanceMeters))
            .ToArray();
        if (eligible.Length < 3 || eligible.Max(effort => effort.DurationSeconds) /\n            eligible.Min(effort => effort.DurationSeconds) < MinimumDurationRatio)
        {
            return null;
        }

        var meanTime = eligible.Average(effort => effort.DurationSeconds);
        var meanDistance = eligible.Average(effort => effort.DistanceMeters);
        var timeVariance = eligible.Sum(effort => Math.Pow(effort.DurationSeconds - meanTime, 2));
        if (timeVariance <= 0 || !double.IsFinite(timeVariance))
        {
            return null;
        }

        var covariance = eligible.Sum(effort =>
            (effort.DurationSeconds - meanTime) * (effort.DistanceMeters - meanDistance));
        var speed = covariance / timeVariance;
        var reserve = meanDistance - speed * meanTime;
        if (!double.IsFinite(speed) || !double.IsFinite(reserve) || speed <= 0 || reserve <= 0)
        {
            return null;
        }

        var rmse = Math.Sqrt(eligible.Average(effort =>
            Math.Pow(effort.DistanceMeters - (speed * effort.DurationSeconds + reserve), 2)));
        return double.IsFinite(rmse) ? new CriticalSpeedFit(speed, reserve, rmse, eligible.Length) : null;
    }

    public static double? CardiovascularEfficiency(double speedMetersPerSecond, double heartRateBpm)
    {
        if (!double.IsFinite(speedMetersPerSecond) || !double.IsFinite(heartRateBpm) ||
            speedMetersPerSecond <= 0 || heartRateBpm <= 0)
        {
            return null;
        }

        var metersPerBeat = speedMetersPerSecond * 60 / heartRateBpm;
        return double.IsFinite(metersPerBeat) ? metersPerBeat : null;
    }

    public static double? ObservedBestSpeed(IEnumerable<PerformanceEffort> efforts, double durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(efforts);
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0)
        {
            return null;
        }

        // Solo finestre della durata richiesta; non ricavare prestazioni brevi da medie di sforzi lunghi.
        return efforts.Where(effort => Math.Abs(effort.DurationSeconds - durationSeconds) <= DurationToleranceSeconds &&
            double.IsFinite(effort.DistanceMeters) && effort.DistanceMeters > 0)
            .Select(effort => effort.DistanceMeters / effort.DurationSeconds)
            .Where(double.IsFinite)
            .Select(speed => (double?)speed)
            .Max();
    }
}
