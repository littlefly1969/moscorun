namespace MoscoRun.Domain.Analysis;

/// <summary>Statistiche circolari dell'orario locale, già normalizzato per fuso e calendario dal chiamante.</summary>
public static class SleepTimingEngine
{
    public static SleepTimingSummary? Summarize(IReadOnlyList<double> localMinutes)
    {
        ArgumentNullException.ThrowIfNull(localMinutes);
        if (localMinutes.Count < 7 || localMinutes.Any(value => !double.IsFinite(value) || value < 0 || value >= 1440))
        {
            return null;
        }

        var cosine = localMinutes.Average(value => Math.Cos(value * Math.Tau / 1440));
        var sine = localMinutes.Average(value => Math.Sin(value * Math.Tau / 1440));
        var resultant = Math.Min(1, Math.Sqrt(cosine * cosine + sine * sine));
        if (resultant < 1e-12)
        {
            return null;
        }

        var angle = (Math.Atan2(sine, cosine) + Math.Tau) % Math.Tau;
        var deviation = Math.Sqrt(-2 * Math.Log(resultant)) * 1440 / Math.Tau;
        return new SleepTimingSummary(localMinutes.Count, angle * 1440 / Math.Tau, deviation);
    }
}
