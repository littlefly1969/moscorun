namespace MoscoRun.Domain.Analysis;

/// <summary>Massimo integrale su finestre esatte interamente supportate dal segnale ZOH nativo.</summary>
public static class ObservedPerformanceEngine
{
    public const string MethodVersion = "observed-curve-native-zoh-v1";

    public static IReadOnlyList<ObservedPerformancePoint> Build(
        IEnumerable<SignalSample> samples, DateTimeOffset start, DateTimeOffset end,
        TimeSpan maxGap, IReadOnlyList<int> durationsSeconds)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(durationsSeconds);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(end, start);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxGap, TimeSpan.Zero);
        if (durationsSeconds.Count > 64 || durationsSeconds.Any(duration => duration <= 0))
        {
            throw new ArgumentException("Durate positive, al massimo 64.", nameof(durationsSeconds));
        }

        var runs = BuildRuns(samples, start, end, maxGap);
        return durationsSeconds.Select(duration => Best(runs, duration)).ToArray();
    }

    private static List<SupportRun> BuildRuns(
        IEnumerable<SignalSample> samples, DateTimeOffset start, DateTimeOffset end, TimeSpan maxGap)
    {
        var slots = samples.Where(item => item.Time >= start && item.Time <= end)
            .GroupBy(item => item.Time).Select(SignalProcessing.Resolve).OrderBy(item => item.Time).ToArray();
        var runs = new List<SupportRun>();
        var boundaries = new List<DateTimeOffset>();
        var integrals = new List<double>();
        for (var i = 0; i + 1 < slots.Length; i++)
        {
            var a = slots[i];
            var b = slots[i + 1];
            if (a.Value is not >= 0 || b.Value is not >= 0)
            {
                Flush(runs, boundaries, integrals);
                continue;
            }

            var duration = b.Time - a.Time;
            var until = duration > maxGap ? a.Time + maxGap : b.Time;
            if (boundaries.Count == 0)
            {
                boundaries.Add(a.Time);
                integrals.Add(0);
            }

            boundaries.Add(until);
            integrals.Add(integrals[^1] + a.Value.GetValueOrDefault() * (until - a.Time).TotalSeconds);
            if (until < b.Time)
            {
                Flush(runs, boundaries, integrals);
            }
        }

        Flush(runs, boundaries, integrals);
        return runs;
    }

    private static void Flush(List<SupportRun> runs, List<DateTimeOffset> boundaries, List<double> integrals)
    {
        if (boundaries.Count >= 2 && double.IsFinite(integrals[^1]))
        {
            runs.Add(new SupportRun(boundaries.ToArray(), integrals.ToArray()));
        }

        boundaries.Clear();
        integrals.Clear();
    }

    private static ObservedPerformancePoint Best(IReadOnlyList<SupportRun> runs, int durationSeconds)
    {
        var duration = TimeSpan.FromSeconds(durationSeconds);
        double? best = null;
        DateTimeOffset? earliest = null;
        foreach (var run in runs)
        {
            var lower = run.Boundaries[0];
            var upper = run.Boundaries[^1];
            if (upper - lower < duration)
            {
                continue;
            }

            var latestStart = upper - duration;
            foreach (var boundary in run.Boundaries)
            {
                // La derivata dell'integrale cambia solo quando un estremo attraversa un confine.
                if (boundary <= latestStart)
                {
                    Evaluate(run, boundary, duration, ref best, ref earliest);
                }

                if (boundary - lower >= duration)
                {
                    Evaluate(run, boundary - duration, duration, ref best, ref earliest);
                }
            }
        }

        return new ObservedPerformancePoint(durationSeconds, best, earliest, earliest + duration);
    }

    private static void Evaluate(
        SupportRun run, DateTimeOffset start, TimeSpan duration, ref double? best, ref DateTimeOffset? earliest)
    {
        var mean = Math.Max(0, (run.IntegralAt(start + duration) - run.IntegralAt(start)) / duration.TotalSeconds);
        if (double.IsFinite(mean) && (!best.HasValue || mean > best || (mean == best && start < earliest)))
        {
            best = mean;
            earliest = start;
        }
    }

    private sealed record SupportRun(DateTimeOffset[] Boundaries, double[] Integrals)
    {
        public double IntegralAt(DateTimeOffset time)
        {
            var index = Array.BinarySearch(Boundaries, time);
            if (index >= 0)
            {
                return Integrals[index];
            }

            var right = ~index;
            var left = right - 1;
            var fraction = (time - Boundaries[left]).TotalSeconds /
                (Boundaries[right] - Boundaries[left]).TotalSeconds;
            return Integrals[left] + fraction * (Integrals[right] - Integrals[left]);
        }
    }
}
