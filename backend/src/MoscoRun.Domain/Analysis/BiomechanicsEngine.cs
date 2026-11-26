namespace MoscoRun.Domain.Analysis;

/// <summary>Indicatori descrittivi, non misure cliniche né obiettivi prescrittivi.</summary>
public static class BiomechanicsEngine
{
    public static double? StepLengthMeters(double speedMetersPerSecond, double stepsPerMinute)
    {
        if (!Positive(speedMetersPerSecond) || !Positive(stepsPerMinute))
        {
            return null;
        }

        var length = speedMetersPerSecond * 60 / stepsPerMinute;
        return double.IsFinite(length) ? length : null;
    }

    public static double? DutyFactorPercent(double contactMilliseconds, double stepsPerMinute)
    {
        if (!Positive(contactMilliseconds) || !Positive(stepsPerMinute))
        {
            return null;
        }

        // Durata del ciclo dello stesso piede: due passi, non uno.
        var factor = contactMilliseconds * stepsPerMinute / 1200;
        return factor <= 100 ? factor : null;
    }

    public static double? VerticalRatioPercent(double oscillationMeters, double stepLengthMeters)
    {
        if (!double.IsFinite(oscillationMeters) || oscillationMeters < 0 || !Positive(stepLengthMeters))
        {
            return null;
        }

        var ratio = 100 * oscillationMeters / stepLengthMeters;
        return double.IsFinite(ratio) ? ratio : null;
    }

    public static double? AsymmetryPercent(double left, double right)
    {
        if (!Positive(left) || !Positive(right))
        {
            return null;
        }

        var mean = left / 2 + right / 2;
        var asymmetry = 100 * ((left - right) / mean);
        return double.IsFinite(asymmetry) ? asymmetry : null;
    }

    public static double? CoefficientOfVariationPercent(IEnumerable<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var values = samples.Where(Positive).ToArray();
        if (values.Length < 2)
        {
            return null;
        }

        var mean = values.Average();
        var deviation = Math.Sqrt(values.Average(value => Math.Pow(value - mean, 2)));
        var variation = 100 * deviation / mean;
        return double.IsFinite(variation) ? variation : null;
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
