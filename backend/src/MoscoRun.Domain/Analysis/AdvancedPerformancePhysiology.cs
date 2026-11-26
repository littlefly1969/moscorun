namespace MoscoRun.Domain.Analysis;

public static class AdvancedPerformancePhysiology
{
    public const string PowerMethodVersion = "work-time-ols-v1";
    public const string CooperMethodVersion = "cooper-12min-field-estimate-v1";

    public static CriticalPowerFit? FitCriticalPower(IEnumerable<CriticalPowerEffort> efforts)
    {
        ArgumentNullException.ThrowIfNull(efforts);
        // Il modello lavoro-tempo condivide la stessa algebra distanza-tempo, non le unità.
        var fit = PerformancePhysiology.FitCriticalSpeed(efforts.Select(effort =>
            new PerformanceEffort(effort.DurationSeconds, effort.DurationSeconds * effort.MeanWatts,
                effort.IsMaximal && double.IsFinite(effort.MeanWatts) && effort.MeanWatts > 0)));
        return fit is null ? null : new CriticalPowerFit(fit.CriticalSpeedMetersPerSecond,
            fit.DistanceReserveMeters, fit.RootMeanSquaredErrorMeters, fit.EffortCount);
    }

    public static CriticalSpeedFit? RobustCriticalSpeed(IEnumerable<PerformanceEffort> efforts)
    {
        ArgumentNullException.ThrowIfNull(efforts);
        var rows = efforts.Where(effort => effort.IsMaximal &&
            effort.DurationSeconds >= PerformancePhysiology.MinimumDurationSeconds &&
            effort.DurationSeconds <= PerformancePhysiology.MaximumDurationSeconds &&
            double.IsFinite(effort.DistanceMeters) && effort.DistanceMeters > 0)
            .GroupBy(effort => Math.Round(effort.DurationSeconds, 3))
            .Select(group => group.MaxBy(effort => effort.DistanceMeters)).ToArray();
        if (rows.Length < 3 || rows.Max(item => item.DurationSeconds) / rows.Min(item => item.DurationSeconds) <
            PerformancePhysiology.MinimumDurationRatio)
        {
            return null;
        }

        var fit = RobustLineRegression.Fit(rows.Select(item => item.DurationSeconds).ToArray(),
            rows.Select(item => item.DistanceMeters).ToArray());
        if (fit is null || fit.Slope <= 0 || fit.Intercept <= 0)
        {
            return null;
        }

        var rmse = Math.Sqrt(rows.Average(item => Math.Pow(item.DistanceMeters - fit.Intercept - fit.Slope * item.DurationSeconds, 2)));
        return double.IsFinite(rmse) ? new CriticalSpeedFit(fit.Slope, fit.Intercept, rmse, rows.Length) : null;
    }

    /// <summary>Stima da protocollo Cooper, mai una misura di laboratorio o inferenza da seduta ordinaria.</summary>
    public static double? CooperVo2MaxEstimate(double distanceMeters, double durationSeconds, bool protocolAttested)
    {
        if (!protocolAttested || !double.IsFinite(distanceMeters) || !double.IsFinite(durationSeconds) ||
            distanceMeters <= 0 || Math.Abs(durationSeconds - 720) > 0.5)
        {
            return null;
        }

        var estimate = 22.351 * (distanceMeters / 1000) - 11.288;
        return double.IsFinite(estimate) && estimate > 0 ? estimate : null;
    }
}
