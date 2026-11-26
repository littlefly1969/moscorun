namespace MoscoRun.Domain.Analysis;

/// <summary>Resampling causale a mantenimento dell'ultimo valore, senza attraversare conflitti o buchi.</summary>
public static class CausalSignalGrid
{
    public const string MethodVersion = "causal-grid-zoh-v1";

    public static IReadOnlyList<SignalSample>? Resample(
        IEnumerable<SignalSample> samples, DateTimeOffset start, DateTimeOffset end, TimeSpan step, TimeSpan maxAge)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (end <= start || step <= TimeSpan.Zero || maxAge < TimeSpan.Zero)
        {
            return null;
        }

        var count = Math.Ceiling((double)(end - start).Ticks / step.Ticks);
        if (count > 1000000)
        {
            return null;
        }

        var slots = samples.Where(sample => sample.Time < end).GroupBy(sample => sample.Time)
            .Select(group => Resolve(group.Key, group)).OrderBy(sample => sample.Time).ToArray();
        var result = new List<SignalSample>((int)count);
        var next = 0;
        SignalSample? latest = null;
        for (var index = 0; index < (int)count; index++)
        {
            var time = start.AddTicks(index * step.Ticks);
            if (time >= end)
            {
                break;
            }

            while (next < slots.Length && slots[next].Time <= time)
            {
                latest = slots[next++];
            }

            var value = latest is { } item && time - item.Time <= maxAge ? item.Value : null;
            result.Add(new SignalSample(time, value));
        }

        return result.AsReadOnly();
    }

    private static SignalSample Resolve(DateTimeOffset time, IEnumerable<SignalSample> group)
    {
        var values = group.Select(sample => sample.Value).Distinct().ToArray();
        return new SignalSample(time, values.Length == 1 && values[0] is { } value && double.IsFinite(value) ? value : null);
    }

    public static double? BestObservedMean(IReadOnlyList<SignalSample> grid, TimeSpan step, int samplesPerWindow)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (step <= TimeSpan.Zero || samplesPerWindow <= 0 || samplesPerWindow > grid.Count)
        {
            return null;
        }

        double sum = 0;
        var consecutive = 0;
        double? best = null;
        for (var index = 0; index < grid.Count; index++)
        {
            var item = grid[index];
            if (index > 0 && item.Time - grid[index - 1].Time != step)
            {
                return null;
            }

            if (item.Value is not { } value || !double.IsFinite(value))
            {
                sum = 0;
                consecutive = 0;
                continue;
            }

            sum += value;
            consecutive++;
            if (consecutive > samplesPerWindow)
            {
                sum -= grid[index - samplesPerWindow].Value.GetValueOrDefault();
            }

            if (consecutive >= samplesPerWindow)
            {
                var mean = sum / samplesPerWindow;
                if (!double.IsFinite(mean))
                {
                    return null;
                }

                best = best.HasValue ? Math.Max(best.Value, mean) : mean;
            }
        }

        return best;
    }
}
