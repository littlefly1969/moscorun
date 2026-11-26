namespace MoscoRun.Domain.Analysis;

/// <summary>Onset operativo e trend robusto; richiede finestre consecutive omogenee già selezionate.</summary>
public static class DurabilityProfileEngine
{
    public const string MethodVersion = "durability-profile-theil-sen-v1";

    public static DurabilityProfile? Analyze(
        IReadOnlyList<DurabilityWindow> windows, double minimumCoverage,
        double onsetLossPercent, int minimumConsecutiveWindows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        if (windows.Count < 3 || windows.Count > 2048 ||
            !double.IsFinite(minimumCoverage) || minimumCoverage <= 0 || minimumCoverage > 1 ||
            !double.IsFinite(onsetLossPercent) || onsetLossPercent <= 0 ||
            minimumConsecutiveWindows < 1 || minimumConsecutiveWindows >= windows.Count)
        {
            return null;
        }

        var first = windows[0];
        var width = first.End - first.Start;
        if (width <= TimeSpan.Zero)
        {
            return null;
        }

        var loss = new double[windows.Count];
        var elapsed = new double[windows.Count];
        double baseline = 0;
        var consecutive = 0;
        DateTimeOffset? onset = null;
        DateTimeOffset? confirmed = null;
        for (var index = 1; index < windows.Count; index++)
        {
            var window = windows[index];
            if (window.Start != windows[index - 1].End || window.End - window.Start != width)
            {
                return null;
            }

            var comparison = DurabilityEngine.Compare(first, window, minimumCoverage);
            if (comparison is null)
            {
                return null;
            }

            baseline = comparison.EarlyEfficiency;
            loss[index] = comparison.EfficiencyLossPercent;
            elapsed[index] = (window.Start - first.Start).TotalHours;
            consecutive = loss[index] >= onsetLossPercent ? consecutive + 1 : 0;
            if (!onset.HasValue && consecutive >= minimumConsecutiveWindows)
            {
                onset = windows[index - minimumConsecutiveWindows + 1].Start;
                confirmed = window.End;
            }
        }

        var trend = RobustLineRegression.Fit(elapsed, loss);
        return trend is not null && double.IsFinite(trend.Slope) && double.IsFinite(trend.MedianAbsoluteResidual)
            ? new DurabilityProfile(first.Start, baseline, Array.AsReadOnly(loss), onset, confirmed,
                trend.Slope, trend.MedianAbsoluteResidual)
            : null;
    }
}
