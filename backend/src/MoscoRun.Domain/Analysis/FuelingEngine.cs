namespace MoscoRun.Domain.Analysis;

/// <summary>Riepilogo di eventi registrati; non misura glicogeno o fabbisogno individuale.</summary>
public static class FuelingEngine
{
    public const string MethodVersion = "observed-fueling-v1";

    public static FuelingSummary? Summarize(
        IEnumerable<NutritionEvent> events, DateTimeOffset start, DateTimeOffset end,
        DateTimeOffset asOf, bool diaryComplete)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (end <= start || end > asOf || !diaryComplete)
        {
            return null;
        }

        var eligible = events.Where(item => item.Time >= start && item.Time < end &&
            item.AvailableAt >= item.Time && item.AvailableAt <= asOf).ToArray();
        var hours = (end - start).TotalHours;
        return new FuelingSummary(eligible.Length,
            Rate(eligible.Select(item => item.CarbohydrateGrams), hours),
            Rate(eligible.Select(item => item.FluidMilliliters), hours),
            Rate(eligible.Select(item => item.SodiumMilligrams), hours));
    }

    /// <summary>Bilancio di massa approssimato: 1 kg di acqua = circa 1 L; condizioni controllate.</summary>
    public static double? EstimatedSweatLitersPerHour(
        double beforeKg, double afterKg, double consumedLiters, double urineLiters, double hours)
    {
        double[] values = [beforeKg, afterKg, consumedLiters, urineLiters, hours];
        if (values.Any(value => !double.IsFinite(value)) || beforeKg <= 0 || afterKg <= 0 ||
            consumedLiters < 0 || urineLiters < 0 || hours <= 0)
        {
            return null;
        }

        var rate = (beforeKg - afterKg + consumedLiters - urineLiters) / hours;
        return double.IsFinite(rate) && rate >= 0 ? rate : null;
    }

    private static double? Rate(IEnumerable<double?> amounts, double hours)
    {
        var values = amounts.ToArray();
        if (values.Any(value => !value.HasValue || !double.IsFinite(value.Value) || value.Value < 0))
        {
            return null;
        }

        var rate = values.Sum(value => value.GetValueOrDefault()) / hours;
        return double.IsFinite(rate) ? rate : null;
    }
}
