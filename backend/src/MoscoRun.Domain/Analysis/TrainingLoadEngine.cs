namespace MoscoRun.Domain.Analysis;

/// <summary>Carichi descrittivi; non stima probabilità di infortunio.</summary>
public static class TrainingLoadEngine
{
    public static double? SessionRpe(double durationMinutes, double? rpe)
    {
        if (!double.IsFinite(durationMinutes) || durationMinutes <= 0 ||
            rpe is not { } rating || !double.IsFinite(rating) || rating < 0 || rating > 10)
        {
            return null;
        }

        var load = durationMinutes * rating;
        return double.IsFinite(load) ? load : null;
    }

    public static double? HeartRateReserveLoad(
        double durationMinutes, double meanBpm, double restingBpm, double maximumBpm)
    {
        if (!double.IsFinite(durationMinutes) || !double.IsFinite(meanBpm) ||
            !double.IsFinite(restingBpm) || !double.IsFinite(maximumBpm) ||
            durationMinutes <= 0 || restingBpm <= 0 || maximumBpm <= restingBpm ||
            meanBpm < restingBpm || meanBpm > maximumBpm)
        {
            return null;
        }

        return durationMinutes * ((meanBpm - restingBpm) / (maximumBpm - restingBpm));
    }

    public static double? DistanceIncreasePercent(double currentMeters, double? priorLongestMeters)
    {
        if (!double.IsFinite(currentMeters) || currentMeters <= 0 ||
            priorLongestMeters is not { } previous || !double.IsFinite(previous) || previous <= 0)
        {
            return null;
        }

        var change = 100 * (currentMeters / previous - 1);
        return double.IsFinite(change) ? change : null;
    }

    public static TrainingLoadSummary? SummarizeWeek(IReadOnlyList<double?> dailyLoads)
    {
        ArgumentNullException.ThrowIfNull(dailyLoads);
        if (dailyLoads.Count != 7 || dailyLoads.Any(load => !load.HasValue ||
            !double.IsFinite(load.Value) || load.Value < 0))
        {
            return null;
        }

        var values = dailyLoads.Select(load => load.GetValueOrDefault()).ToArray();
        var total = values.Sum();
        var mean = total / 7;
        var deviation = Math.Sqrt(values.Average(load => Math.Pow(load - mean, 2)));
        if (!double.IsFinite(total) || !double.IsFinite(deviation))
        {
            return null;
        }

        double? monotony = deviation > 0 ? mean / deviation : null;
        var strain = monotony * total;
        return new TrainingLoadSummary(total, mean,
            monotony is { } m && double.IsFinite(m) ? m : null,
            strain is { } s && double.IsFinite(s) ? s : null);
    }
}
