namespace MoscoRun.Domain.Analysis;

/// <summary>Selezione della costante di tempo da griglia dichiarata, con equilibrio misurato o stimato separatamente.</summary>
public static class CardiacResponseCalibration
{
    public static CardiacResponseFit? Fit(
        IReadOnlyList<CardiacResponsePoint> points, double initialBpm, double equilibriumBpm, IEnumerable<double> candidates)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(candidates);
        if (points.Count < 4 || !Positive(initialBpm) || !Positive(equilibriumBpm) ||
            Math.Abs(initialBpm - equilibriumBpm) < 1 ||
            points.Any(point => !Positive(point.ElapsedSeconds) || !Positive(point.HeartRateBpm)))
        {
            return null;
        }

        for (var index = 1; index < points.Count; index++)
        {
            if (points[index].ElapsedSeconds <= points[index - 1].ElapsedSeconds)
            {
                return null;
            }
        }

        CardiacResponseFit? best = null;
        foreach (var tau in candidates.Distinct().Order())
        {
            if (!Positive(tau))
            {
                return null;
            }

            var rmse = Math.Sqrt(points.Average(point =>
            {
                var prediction = equilibriumBpm + (initialBpm - equilibriumBpm) * Math.Exp(-point.ElapsedSeconds / tau);
                return Math.Pow(point.HeartRateBpm - prediction, 2);
            }));
            if (double.IsFinite(rmse) && (best is null || rmse < best.RootMeanSquaredErrorBpm))
            {
                best = new CardiacResponseFit(tau, rmse, points.Count);
            }
        }

        return best;
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
