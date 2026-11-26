namespace MoscoRun.Domain.Analysis;

/// <summary>Costruisce finestre su supporto temporale comune, senza interpolazione o imputazione.</summary>
public static class PairedDurabilityEngine
{
    public const string MethodVersion = "paired-zoh-intersection-v1";

    public static DurabilityWindow? BuildWindow(
        Guid sessionId, ExternalLoadMetric metric, IEnumerable<SignalSample> externalLoad,
        IEnumerable<SignalSample> heartRate, DateTimeOffset start, DateTimeOffset end, TimeSpan maxGap)
    {
        ArgumentNullException.ThrowIfNull(externalLoad);
        ArgumentNullException.ThrowIfNull(heartRate);
        if (sessionId == Guid.Empty || metric is not (ExternalLoadMetric.Speed or ExternalLoadMetric.Power) ||
            end <= start || maxGap <= TimeSpan.Zero)
        {
            return null;
        }

        var load = Supports(externalLoad, start, end, maxGap);
        var heart = Supports(heartRate, start, end, maxGap);
        var i = 0;
        var j = 0;
        long ticks = 0;
        double loadIntegral = 0;
        double heartIntegral = 0;
        while (i < load.Count && j < heart.Count)
        {
            var a = load[i];
            var b = heart[j];
            var from = a.Start > b.Start ? a.Start : b.Start;
            var until = a.End < b.End ? a.End : b.End;
            if (until > from)
            {
                var duration = until - from;
                ticks += duration.Ticks;
                loadIntegral += a.Value * duration.TotalSeconds;
                heartIntegral += b.Value * duration.TotalSeconds;
            }

            if (a.End <= b.End)
            {
                i++;
            }

            if (b.End <= a.End)
            {
                j++;
            }
        }

        var support = TimeSpan.FromTicks(ticks);
        if (ticks == 0 || !double.IsFinite(loadIntegral) || !double.IsFinite(heartIntegral))
        {
            return null;
        }

        var meanLoad = loadIntegral / support.TotalSeconds;
        var meanHeart = heartIntegral / support.TotalSeconds;
        return double.IsFinite(meanLoad) && double.IsFinite(meanHeart) && meanLoad > 0 && meanHeart > 0
            ? new DurabilityWindow(start, end, support, meanLoad, meanHeart, sessionId, metric)
            : null;
    }

    private static List<(DateTimeOffset Start, DateTimeOffset End, double Value)> Supports(
        IEnumerable<SignalSample> samples, DateTimeOffset start, DateTimeOffset end, TimeSpan maxGap)
    {
        var slots = samples.Where(sample => sample.Time <= end)
            .GroupBy(sample => sample.Time).Select(SignalProcessing.Resolve).OrderBy(sample => sample.Time).ToArray();
        var result = new List<(DateTimeOffset Start, DateTimeOffset End, double Value)>();
        for (var index = 0; index + 1 < slots.Length; index++)
        {
            var current = slots[index];
            var next = slots[index + 1];
            // Campioni nulli, in conflitto, non fisici o pause interrompono il supporto.
            if (current.Value is not { } value || value <= 0 || next.Value is not > 0)
            {
                continue;
            }

            var supported = next.Time - current.Time;
            var until = supported > maxGap ? current.Time.Add(maxGap) : next.Time;
            var from = current.Time > start ? current.Time : start;
            if (until > from)
            {
                result.Add((from, until, value));
            }
        }

        return result;
    }
}
