namespace MoscoRun.Domain.Analysis;

/// <summary>Disaccoppiamento descrittivo per due finestre comparabili della stessa seduta e metrica.</summary>
public static class DurabilityEngine
{
    public const string MethodVersion = "paired-window-efficiency-loss-v2";

    public static DurabilityComparison? Compare(
        DurabilityWindow early, DurabilityWindow late, double minimumCoverage)
    {
        if (!double.IsFinite(minimumCoverage) || minimumCoverage <= 0 || minimumCoverage > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumCoverage));
        }

        if (early.SessionId != late.SessionId || early.ExternalLoadMetric != late.ExternalLoadMetric ||
            early.End > late.Start || !IsValid(early, minimumCoverage) || !IsValid(late, minimumCoverage))
        {
            return null;
        }

        var first = early.MeanExternalLoad / early.MeanHeartRateBpm;
        var second = late.MeanExternalLoad / late.MeanHeartRateBpm;
        var loss = (1 - second / first) * 100;
        if (!double.IsFinite(first) || !double.IsFinite(second) || !double.IsFinite(loss) || first <= 0 || second <= 0)
        {
            return null;
        }

        return new DurabilityComparison(first, second, loss, Coverage(early), Coverage(late));
    }

    private static double Coverage(DurabilityWindow window) =>
        window.PairedDuration.TotalSeconds / (window.End - window.Start).TotalSeconds;

    private static bool IsValid(DurabilityWindow window, double minimumCoverage) =>
        window.SessionId != Guid.Empty &&
        window.ExternalLoadMetric is ExternalLoadMetric.Speed or ExternalLoadMetric.Power &&
        window.End > window.Start && window.PairedDuration > TimeSpan.Zero &&
        window.PairedDuration <= window.End - window.Start &&
        double.IsFinite(window.MeanExternalLoad) && window.MeanExternalLoad > 0 &&
        double.IsFinite(window.MeanHeartRateBpm) && window.MeanHeartRateBpm > 0 &&
        Coverage(window) >= minimumCoverage;
}
