using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

/// <summary>Stima cinematica su supporto comune; non misura la lunghezza anatomica o l'asimmetria del passo.</summary>
public static class RunningStepEngine
{
    public const string MethodVersion = "running-step-native-paired-zoh-v1";
    private static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(5);

    public static RunningStepReport? Build(Guid ownerId, Workout workout, IReadOnlyList<WorkoutSeries> series,
        DateTimeOffset asOf, bool cadenceIsStepsPerMinute)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(series);
        if (ownerId == Guid.Empty || ownerId != workout.OwnerId || workout.UpdatedAt > asOf ||
            workout.EndedAt > asOf || workout.EndedAt <= workout.StartedAt ||
            series.Any(item => item.WorkoutId != workout.Id) ||
            series.Select(item => item.Metric).Distinct().Count() != series.Count)
        {
            return null;
        }

        if (!string.Equals(workout.ActivityType, "RUNNING", StringComparison.OrdinalIgnoreCase))
        {
            return Report(workout, asOf, cadenceIsStepsPerMinute, "unsupported-activity", []);
        }

        if (!cadenceIsStepsPerMinute)
        {
            return Report(workout, asOf, false, "unconfirmed-cadence-unit", []);
        }

        var speed = series.SingleOrDefault(item => item.Metric == SeriesMetric.Speed);
        var cadence = series.SingleOrDefault(item => item.Metric == SeriesMetric.Cadence);
        if (speed is null || cadence is null)
        {
            return Report(workout, asOf, true, "missing-channel", []);
        }

        var pairs = Pair(Support(speed, workout), Support(cadence, workout));
        return Report(workout, asOf, true, pairs.Count == 0 ? "insufficient-support" : "observed", pairs);
    }

    private static RunningStepReport Report(Workout workout, DateTimeOffset asOf, bool confirmed,
        string status, IReadOnlyList<PairedStep> pairs)
    {
        var duration = (workout.EndedAt - workout.StartedAt).TotalSeconds;
        var support = pairs.Sum(pair => pair.Seconds);
        var speed = Moments(pairs, pair => pair.Speed, support);
        var cadence = Moments(pairs, pair => pair.Cadence, support);
        var step = Moments(pairs, pair => pair.StepLength, support);
        return new RunningStepReport(workout.Id, workout.Revision, Convert.ToHexStringLower(workout.ContentHash),
            workout.Source, asOf, MethodVersion, status, MaxGap.TotalSeconds, confirmed, duration,
            support, support / duration, speed.Mean, cadence.Mean, step.Mean, step.Cv, cadence.Cv);
    }

    private static (double? Mean, double? Cv) Moments(IReadOnlyList<PairedStep> pairs,
        Func<PairedStep, double> select, double seconds)
    {
        if (seconds <= 0)
        {
            return (null, null);
        }

        var mean = pairs.Sum(pair => pair.Seconds * select(pair)) / seconds;
        if (!double.IsFinite(mean) || mean <= 0)
        {
            return (null, null);
        }

        var variance = pairs.Sum(pair => pair.Seconds * Math.Pow(select(pair) - mean, 2)) / seconds;
        var cv = 100 * Math.Sqrt(variance) / mean;
        return (mean, pairs.Count >= 2 && double.IsFinite(cv) ? cv : null);
    }

    private static List<SupportSlice> Support(WorkoutSeries series, Workout workout)
    {
        var slots = series.Samples().Select(item => new SignalSample(item.Time, item.Value))
            .Where(item => item.Time >= workout.StartedAt && item.Time <= workout.EndedAt)
            .GroupBy(item => item.Time).Select(SignalProcessing.Resolve).OrderBy(item => item.Time).ToArray();
        var result = new List<SupportSlice>();
        for (var index = 0; index + 1 < slots.Length; index++)
        {
            var left = slots[index];
            var right = slots[index + 1];
            if (left.Value is not > 0 || right.Value is not > 0)
            {
                continue;
            }

            var end = right.Time - left.Time > MaxGap ? left.Time + MaxGap : right.Time;
            result.Add(new SupportSlice(left.Time, end, left.Value.GetValueOrDefault()));
        }

        return result;
    }

    private static List<PairedStep> Pair(List<SupportSlice> speed, List<SupportSlice> cadence)
    {
        var result = new List<PairedStep>();
        var i = 0;
        var j = 0;
        while (i < speed.Count && j < cadence.Count)
        {
            var a = speed[i];
            var b = cadence[j];
            var start = a.Start > b.Start ? a.Start : b.Start;
            var end = a.End < b.End ? a.End : b.End;
            if (end > start && BiomechanicsEngine.StepLengthMeters(a.Value, b.Value) is { } length)
            {
                result.Add(new PairedStep((end - start).TotalSeconds, a.Value, b.Value, length));
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

        return result;
    }

    private sealed record SupportSlice(DateTimeOffset Start, DateTimeOffset End, double Value);
    private sealed record PairedStep(double Seconds, double Speed, double Cadence, double StepLength);
}
