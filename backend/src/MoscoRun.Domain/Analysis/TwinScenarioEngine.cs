namespace MoscoRun.Domain.Analysis;

public static class TwinScenarioEngine
{
    public static TwinComparison? Compare(IReadOnlyList<TwinSegmentResult> baseline, IReadOnlyList<TwinSegmentResult> alternative)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(alternative);
        if (!Valid(baseline) || !Valid(alternative) ||
            Math.Abs(baseline[^1].CumulativeSeconds - alternative[^1].CumulativeSeconds) > 0.001)
        {
            return null;
        }

        return new TwinComparison(alternative[^1].CumulativeMeters - baseline[^1].CumulativeMeters,
            alternative[^1].EndHeartRateBpm - baseline[^1].EndHeartRateBpm,
            WeightedMean(alternative) - WeightedMean(baseline));
    }

    private static bool Valid(IReadOnlyList<TwinSegmentResult> results)
    {
        double lastTime = 0;
        double lastDistance = 0;
        foreach (var item in results)
        {
            if (!double.IsFinite(item.CumulativeSeconds) || item.CumulativeSeconds <= lastTime ||
                !double.IsFinite(item.CumulativeMeters) || item.CumulativeMeters < lastDistance ||
                !double.IsFinite(item.EndHeartRateBpm) || item.EndHeartRateBpm <= 0 ||
                !double.IsFinite(item.MeanHeartRateBpm) || item.MeanHeartRateBpm <= 0)
            {
                return false;
            }

            lastTime = item.CumulativeSeconds;
            lastDistance = item.CumulativeMeters;
        }

        return results.Count > 0;
    }

    private static double WeightedMean(IReadOnlyList<TwinSegmentResult> results)
    {
        double last = 0;
        double mean = 0;
        var total = results[^1].CumulativeSeconds;
        foreach (var item in results)
        {
            mean += item.MeanHeartRateBpm * ((item.CumulativeSeconds - last) / total);
            last = item.CumulativeSeconds;
        }

        return mean;
    }
}
