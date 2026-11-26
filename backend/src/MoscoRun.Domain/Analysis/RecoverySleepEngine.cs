namespace MoscoRun.Domain.Analysis;

/// <summary>Confronti intra-atleta, non un punteggio clinico o una prescrizione.</summary>
public static class RecoverySleepEngine
{
    public static RecoveryDeviation? CompareWithBaseline(double current, IEnumerable<double> priorValues)
    {
        ArgumentNullException.ThrowIfNull(priorValues);
        var prior = priorValues.Where(double.IsFinite).ToArray();
        if (!double.IsFinite(current) || prior.Length < 7)
        {
            return null;
        }

        var median = SignalProcessing.Median(prior);
        if (median is not { } center)
        {
            return null;
        }

        var mad = SignalProcessing.Median(prior.Select(value => Math.Abs(value - center)));
        if (mad is not { } spread)
        {
            return null;
        }

        var robustZ = spread > 0 ? 0.6744897501960817 * ((current - center) / spread) : double.NaN;
        return new RecoveryDeviation(prior.Length, center, spread, double.IsFinite(robustZ) ? robustZ : null);
    }

    public static double? SleepEfficiencyPercent(double asleepMinutes, double inBedMinutes)
    {
        if (!double.IsFinite(asleepMinutes) || !double.IsFinite(inBedMinutes) ||
            asleepMinutes < 0 || inBedMinutes <= 0 || asleepMinutes > inBedMinutes || inBedMinutes > 1440)
        {
            return null;
        }

        return 100 * (asleepMinutes / inBedMinutes);
    }

    public static double? CircularTimingDifferenceMinutes(double firstMinuteOfDay, double secondMinuteOfDay)
    {
        if (!ValidMinute(firstMinuteOfDay) || !ValidMinute(secondMinuteOfDay))
        {
            return null;
        }

        var difference = Math.Abs(firstMinuteOfDay - secondMinuteOfDay);
        return Math.Min(difference, 1440 - difference);
    }

    public static double? SleepPerformanceCorrelation(IReadOnlyList<double> sleepHours, IReadOnlyList<double> outcomes)
    {
        ArgumentNullException.ThrowIfNull(sleepHours);
        ArgumentNullException.ThrowIfNull(outcomes);
        if (sleepHours.Count < 7 || sleepHours.Count != outcomes.Count ||
            sleepHours.Any(hours => !double.IsFinite(hours) || hours < 0 || hours > 24) ||
            outcomes.Any(outcome => !double.IsFinite(outcome)))
        {
            return null;
        }

        var sleepMean = sleepHours.Average();
        var outcomeMean = outcomes.Average();
        var sleepDeviation = sleepHours.Sum(hours => Math.Pow(hours - sleepMean, 2));
        var outcomeDeviation = outcomes.Sum(outcome => Math.Pow(outcome - outcomeMean, 2));
        var denominator = Math.Sqrt(sleepDeviation) * Math.Sqrt(outcomeDeviation);
        if (!double.IsFinite(denominator) || denominator <= 0)
        {
            return null;
        }

        var numerator = sleepHours.Zip(outcomes, (hours, outcome) =>
            (hours - sleepMean) * (outcome - outcomeMean)).Sum();
        var correlation = numerator / denominator;
        return double.IsFinite(correlation) ? Math.Clamp(correlation, -1, 1) : null;
    }

    private static bool ValidMinute(double value) => double.IsFinite(value) && value >= 0 && value < 1440;
}
