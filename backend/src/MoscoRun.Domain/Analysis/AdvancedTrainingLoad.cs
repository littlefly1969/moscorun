namespace MoscoRun.Domain.Analysis;

/// <summary>Carichi descrittivi su protocolli omogenei; non soglie di rischio infortunio.</summary>
public static class AdvancedTrainingLoad
{
    public const string TrimpMethodVersion = "exponential-trimp-explicit-coefficients-v1";

    public static double? ExponentialTrimp(
        double durationMinutes, double meanBpm, double restingBpm, double maximumBpm, double a, double b)
    {
        var reserveLoad = TrainingLoadEngine.HeartRateReserveLoad(durationMinutes, meanBpm, restingBpm, maximumBpm);
        if (reserveLoad is not { } load || !double.IsFinite(a) || !double.IsFinite(b) || a <= 0 || b <= 0)
        {
            return null;
        }

        var trimp = load * a * Math.Exp(b * (load / durationMinutes));
        return double.IsFinite(trimp) ? trimp : null;
    }

    public static IntensityDistribution? Distribute(
        IReadOnlyList<IntensitySlice> slices, IReadOnlyList<double> upperBoundaries)
    {
        ArgumentNullException.ThrowIfNull(slices);
        ArgumentNullException.ThrowIfNull(upperBoundaries);
        if (slices.Count == 0 || upperBoundaries.Count == 0 ||
            upperBoundaries.Any(value => !double.IsFinite(value) || value <= 0))
        {
            return null;
        }

        for (var index = 1; index < upperBoundaries.Count; index++)
        {
            if (upperBoundaries[index] <= upperBoundaries[index - 1])
            {
                return null;
            }
        }

        var zones = new double[upperBoundaries.Count + 1];
        double missing = 0;
        foreach (var slice in slices)
        {
            if (!double.IsFinite(slice.Minutes) || slice.Minutes <= 0 ||
                (slice.Intensity is { } intensity && (!double.IsFinite(intensity) || intensity < 0)))
            {
                return null;
            }

            if (slice.Intensity is not { } value)
            {
                missing += slice.Minutes;
                continue;
            }

            var zone = 0;
            while (zone < upperBoundaries.Count && value >= upperBoundaries[zone])
            {
                zone++;
            }

            zones[zone] += slice.Minutes;
        }

        var observed = zones.Sum();
        var total = observed + missing;
        return double.IsFinite(total) && total > 0
            ? new IntensityDistribution(Array.AsReadOnly(zones), missing, total, observed / total) : null;
    }
}
