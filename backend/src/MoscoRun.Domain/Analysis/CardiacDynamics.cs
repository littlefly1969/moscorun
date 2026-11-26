namespace MoscoRun.Domain.Analysis;

/// <summary>Analisi descrittiva della frequenza cardiaca e di intervalli RR consecutivi validati.</summary>
public static class CardiacDynamics
{
    public const string HrvMethodVersion = "rr-summary-v2";
    public const double MinimumRrMilliseconds = 300;
    public const double MaximumRrMilliseconds = 2000;
    public const double MaximumRelativeRrChange = 0.25;

    public static double? HeartRateRecovery(double endBpm, double recoveryBpm)
    {
        if (!Positive(endBpm) || !Positive(recoveryBpm))
        {
            return null;
        }

        return endBpm - recoveryBpm;
    }

    public static HrvSummary? SummarizeRr(IReadOnlyList<double> intervalsMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(intervalsMilliseconds);
        if (ValidateRr(intervalsMilliseconds) is not null)
        {
            return null;
        }

        // Non rimuovere intervalli invalidi: creerebbe coppie successive artificiali.
        var mean = intervalsMilliseconds.Average();
        double squaredDifferences = 0;
        for (var index = 1; index < intervalsMilliseconds.Count; index++)
        {
            squaredDifferences += Math.Pow(intervalsMilliseconds[index] - intervalsMilliseconds[index - 1], 2);
        }

        var rmssd = Math.Sqrt(squaredDifferences / (intervalsMilliseconds.Count - 1));
        var sdnn = Math.Sqrt(intervalsMilliseconds.Sum(interval => Math.Pow(interval - mean, 2)) /
            (intervalsMilliseconds.Count - 1));
        return double.IsFinite(mean) && double.IsFinite(rmssd) && double.IsFinite(sdnn)
            ? new HrvSummary(intervalsMilliseconds.Count, mean, rmssd, sdnn)
            : null;
    }

    public static string? ValidateRr(IReadOnlyList<double> intervalsMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(intervalsMilliseconds);
        if (intervalsMilliseconds.Count < 3)
        {
            return "insufficient-intervals";
        }

        for (var index = 0; index < intervalsMilliseconds.Count; index++)
        {
            var current = intervalsMilliseconds[index];
            if (!double.IsFinite(current) || current < MinimumRrMilliseconds || current > MaximumRrMilliseconds)
            {
                return "rr-out-of-range";
            }

            if (index > 0 && Math.Abs(current - intervalsMilliseconds[index - 1]) /
                intervalsMilliseconds[index - 1] > MaximumRelativeRrChange)
            {
                return "rr-abrupt-change";
            }
        }

        return null;
    }

    public static double? FirstOrderResponse(double initialBpm, double equilibriumBpm, double elapsedSeconds, double timeConstantSeconds)
    {
        if (!Positive(initialBpm) || !Positive(equilibriumBpm) || !Positive(timeConstantSeconds) ||
            !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            return null;
        }

        var response = equilibriumBpm + (initialBpm - equilibriumBpm) * Math.Exp(-elapsedSeconds / timeConstantSeconds);
        return double.IsFinite(response) ? response : null;
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
