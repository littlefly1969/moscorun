namespace MoscoRun.Domain.Analysis;

/// <summary>Contabilità di massa; le stime BIA non diventano misure dirette.</summary>
public static class BodyCompositionEngine
{
    public static double? FatFreeMassKg(double weightKg, double fatPercentage)
    {
        if (!double.IsFinite(weightKg) || !double.IsFinite(fatPercentage) || weightKg <= 0 ||
            fatPercentage < 0 || fatPercentage >= 100)
        {
            return null;
        }

        return weightKg * (1 - fatPercentage / 100);
    }

    public static double? ChangePercent(double currentKg, double previousKg)
    {
        if (!double.IsFinite(currentKg) || !double.IsFinite(previousKg) || currentKg <= 0 || previousKg <= 0)
        {
            return null;
        }

        var change = 100 * (currentKg / previousKg - 1);
        return double.IsFinite(change) ? change : null;
    }

    /// <summary>Trend robusto su misure omogenee e già autorizzate, non stima della perdita di grasso.</summary>
    public static double? MedianWeight(IEnumerable<double> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var eligible = weights.Where(value => double.IsFinite(value) && value > 0).ToArray();
        return eligible.Length >= 3 ? SignalProcessing.Median(eligible) : null;
    }
}
