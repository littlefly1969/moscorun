namespace MoscoRun.Domain.Analysis;

/// <summary>Descrittori temporali su intersezione esatta del supporto osservato; non misure per singolo passo.</summary>
public static class ExtendedBiomechanicsEngine
{
    public const string MethodVersion = "extended-biomechanics-paired-support-v1";
    public const double MaximumGapSeconds = 5;

    public static bool IsSupported(string metric) => metric is "speed" or "cadence" or "groundContactTime" or "verticalOscillation" or "groundContactBalance";

    public static IReadOnlyList<BiomechanicsMetricSummary>? Build(DateTimeOffset start, DateTimeOffset end,
        IReadOnlyList<BiomechanicsChannel> channels, bool cadenceIsStepsPerMinute, bool balanceIsLeftPercent)
    {
        ArgumentNullException.ThrowIfNull(channels);
        if (end <= start || channels.Count > 5 || channels.Any(channel => !IsSupported(channel.Metric)) ||
            channels.Select(channel => channel.Metric).Distinct(StringComparer.Ordinal).Count() != channels.Count)
        {
            return null;
        }

        var support = channels.ToDictionary(channel => channel.Metric,
            channel => Support(channel, start, end), StringComparer.Ordinal);
        var duration = (end - start).TotalSeconds;
        var cadence = cadenceIsStepsPerMinute ? support.GetValueOrDefault("cadence") ?? [] : [];
        var speed = support.GetValueOrDefault("speed") ?? [];
        var contact = support.GetValueOrDefault("groundContactTime") ?? [];
        var vertical = support.GetValueOrDefault("verticalOscillation") ?? [];
        var balance = balanceIsLeftPercent ? support.GetValueOrDefault("groundContactBalance") ?? [] : [];
        var step = Combine(speed, cadence, BiomechanicsEngine.StepLengthMeters);
        var duty = Combine(contact, cadence, BiomechanicsEngine.DutyFactorPercent);
        var ratio = Combine(vertical, step, BiomechanicsEngine.VerticalRatioPercent);
        var asymmetry = Combine(balance, balance, (left, _) => BiomechanicsEngine.AsymmetryPercent(left, 100 - left));
        return Array.AsReadOnly(new[]
        {
            Summary("groundContactTime", "ms", contact, duration),
            Summary("verticalOscillation", "m", vertical, duration),
            Summary("stepLength", "m", step, duration),
            Summary("dutyFactor", "%", duty, duration),
            Summary("verticalRatio", "%", ratio, duration),
            Summary("groundContactBalance", "%", balance, duration),
            Summary("groundContactTimeAsymmetry", "%", asymmetry, duration),
        });
    }

    private static Slice[] Support(BiomechanicsChannel channel, DateTimeOffset start, DateTimeOffset end)
    {
        var slots = channel.Samples.Where(sample => sample.Time >= start && sample.Time <= end)
            .GroupBy(sample => sample.Time).Select(SignalProcessing.Resolve).OrderBy(sample => sample.Time).ToArray();
        var result = new List<Slice>();
        for (var i = 1; i < slots.Length; i++)
        {
            var left = slots[i - 1];
            var right = slots[i];
            if (!Valid(channel.Metric, left.Value) || !Valid(channel.Metric, right.Value) ||
                (right.Time - left.Time).TotalSeconds > MaximumGapSeconds)
            {
                continue;
            }

            result.Add(new Slice(left.Time, right.Time, left.Value.GetValueOrDefault()));
        }

        return result.ToArray();
    }

    private static bool Valid(string metric, double? value) => value is { } number && double.IsFinite(number) &&
        (metric switch
        {
            "groundContactBalance" => number is > 0 and < 100,
            "verticalOscillation" => number >= 0,
            _ => number > 0,
        });

    private static Slice[] Combine(Slice[] first, Slice[] second, Func<double, double, double?> derive)
    {
        var result = new List<Slice>();
        var i = 0;
        var j = 0;
        while (i < first.Length && j < second.Length)
        {
            var a = first[i];
            var b = second[j];
            var start = a.Start > b.Start ? a.Start : b.Start;
            var end = a.End < b.End ? a.End : b.End;
            if (end > start && derive(a.Value, b.Value) is { } value && double.IsFinite(value))
            {
                result.Add(new Slice(start, end, value));
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

        return result.ToArray();
    }

    private static BiomechanicsMetricSummary Summary(string metric, string unit, Slice[] slices, double duration)
    {
        var seconds = slices.Sum(slice => slice.Seconds);
        if (seconds <= 0)
        {
            return new BiomechanicsMetricSummary(metric, unit, 0, 0, null, null);
        }

        var mean = slices.Sum(slice => slice.Seconds * slice.Value) / seconds;
        var variance = slices.Sum(slice => slice.Seconds * Math.Pow(slice.Value - mean, 2)) / seconds;
        var cv = 100 * Math.Sqrt(variance) / mean;
        return new BiomechanicsMetricSummary(metric, unit, seconds, seconds / duration,
            double.IsFinite(mean) ? mean : null, slices.Length > 1 && mean > 0 &&
            !string.Equals(metric, "groundContactTimeAsymmetry", StringComparison.Ordinal) && double.IsFinite(cv) ? cv : null);
    }

    private sealed record Slice(DateTimeOffset Start, DateTimeOffset End, double Value)
    {
        public double Seconds => (End - Start).TotalSeconds;
    }
}
