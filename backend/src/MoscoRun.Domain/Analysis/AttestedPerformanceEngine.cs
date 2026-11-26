namespace MoscoRun.Domain.Analysis;

/// <summary>Prestazioni di campo da prove disponibili nel solo blocco di fit, non misure di laboratorio.</summary>
public static class AttestedPerformanceEngine
{
    public const string RobustMethodVersion = "distance-time-theil-sen-v1";

    public static AttestedPerformanceProfile Build(IReadOnlyList<RaceCalibrationEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var training = evidence.Where(item => string.Equals(item.Role, "training", StringComparison.Ordinal)).ToArray();
        var candidates = training.Where(item => item.DurationSeconds >= PerformancePhysiology.MinimumDurationSeconds &&
            item.DurationSeconds <= PerformancePhysiology.MaximumDurationSeconds &&
            double.IsFinite(item.DistanceMeters) && item.DistanceMeters > 0).ToArray();
        var efforts = candidates.Select(item => new PerformanceEffort(item.DurationSeconds, item.DistanceMeters, true)).ToArray();
        var cooper = training.Select(Cooper).OfType<CooperTestResult>().ToArray();
        var power = candidates.Where(item => item.MeanPowerWatts is > 0 && double.IsFinite(item.MeanPowerWatts.Value)).ToArray();
        var powerFit = AdvancedPerformancePhysiology.FitCriticalPower(power.Select(item =>
            new CriticalPowerEffort(item.DurationSeconds, item.MeanPowerWatts.GetValueOrDefault(), true)));
        return new AttestedPerformanceProfile(PerformancePhysiology.MethodVersion, RobustMethodVersion,
            AdvancedPerformancePhysiology.CooperMethodVersion, PerformancePhysiology.FitCriticalSpeed(efforts),
            AdvancedPerformancePhysiology.RobustCriticalSpeed(efforts), Array.AsReadOnly(cooper),
            Array.AsReadOnly(candidates.Select(item => item.WorkoutId).ToArray()), AdvancedPerformancePhysiology.PowerMethodVersion,
            powerFit, Array.AsReadOnly(power.Select(item => item.WorkoutId).ToArray()));
    }

    private static CooperTestResult? Cooper(RaceCalibrationEvidence item)
    {
        var estimate = AdvancedPerformancePhysiology.CooperVo2MaxEstimate(item.DistanceMeters, item.DurationSeconds, true);
        return estimate is { } value ? new CooperTestResult(item.WorkoutId, item.Revision, item.EndedAt,
            item.DistanceMeters, item.DurationSeconds, value) : null;
    }
}
