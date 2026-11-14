namespace MoscoRun.Domain.Analysis;

/// <summary>
/// Tendenza del peso su medie mobili: le singole pesate oscillano di ±1 kg per idratazione,
/// il confronto fra medie è il segnale utile.
/// </summary>
public static class WeightTrend
{
    public const int SmoothingDays = 7;

    /// <summary>Media delle pesate nella finestra di <see cref="SmoothingDays"/> giorni che termina in <paramref name="at"/>; null se vuota.</summary>
    public static decimal? MovingAverage(IReadOnlyCollection<WeightPoint> points, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(points);
        var start = at - TimeSpan.FromDays(SmoothingDays);
        decimal sum = 0;
        var count = 0;
        foreach (var p in points)
        {
            if (p.MeasuredAt > start && p.MeasuredAt <= at)
            {
                sum += p.WeightKg;
                count++;
            }
        }

        return count == 0 ? null : Math.Round(sum / count, 2, MidpointRounding.ToEven);
    }

    /// <summary>Variazione fra la media attuale e quella di <paramref name="period"/> fa; null se manca una delle due.</summary>
    public static decimal? Change(IReadOnlyCollection<WeightPoint> points, DateTimeOffset now, TimeSpan period)
    {
        var current = MovingAverage(points, now);
        var previous = MovingAverage(points, now - period);
        return current is null || previous is null ? null : current - previous;
    }
}
