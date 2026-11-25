namespace MoscoRun.Domain.Analysis;

/// <summary>Primitive per singolo canale e seduta. Non interpolano attraverso campioni in conflitto.</summary>
public static class SignalProcessing
{
    public static SignalQuality Audit(
        IEnumerable<SignalSample> samples, DateTimeOffset start, DateTimeOffset end, TimeSpan maxGap)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxGap, TimeSpan.Zero);
        var all = samples.ToArray();
        var valid = all.Where(IsFinite).ToArray();
        var inside = valid.Where(sample => sample.Time >= start && sample.Time <= end).ToArray();
        var slots = Group(inside);
        double seconds = 0;
        double weighted = 0;
        for (var i = 0; i + 1 < slots.Length; i++)
        {
            if (slots[i].Value is not { } value || !slots[i + 1].Value.HasValue)
            {
                continue;
            }

            var duration = Math.Min((slots[i + 1].Time - slots[i].Time).TotalSeconds, maxGap.TotalSeconds);
            seconds += duration;
            weighted += value * duration;
        }

        return new SignalQuality(all.Length, slots.Count(sample => sample.Value.HasValue),
            all.Length - valid.Length, valid.Length - inside.Length, inside.Length - slots.Length,
            slots.Count(sample => !sample.Value.HasValue), TimeSpan.FromSeconds(seconds),
            seconds / (end - start).TotalSeconds, seconds > 0 && double.IsFinite(weighted) ? weighted / seconds : null);
    }

    public static double? CausalValue(
        IEnumerable<SignalSample> samples, DateTimeOffset target, TimeSpan maxAge)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAge, TimeSpan.Zero);
        var slots = Group(samples.Where(sample => sample.Time <= target && IsFinite(sample)));
        if (slots.Length == 0)
        {
            return null;
        }

        var latest = slots[^1];
        return target - latest.Time <= maxAge ? latest.Value : null;
    }

    public static double? Median(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var sorted = values.Where(double.IsFinite).Order().ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }

        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? sorted[middle - 1] / 2 + sorted[middle] / 2 : sorted[middle];
    }

    private static bool IsFinite(SignalSample sample) =>
        sample.Value is { } value && double.IsFinite(value);

    private static SignalSample[] Group(IEnumerable<SignalSample> samples) =>
        samples.GroupBy(sample => sample.Time)
            .Select(group => new SignalSample(group.Key,
                group.Select(sample => sample.Value).Distinct().Count() == 1 ? group.First().Value : null))
            .OrderBy(sample => sample.Time)
            .ToArray();
}
