using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

/// <summary>Distribuzione temporale su supporto osservato; non imputa pause, lacune o parametri fisiologici.</summary>
public static class WorkoutIntensityEngine
{
    public const string MethodVersion = "workout-intensity-native-support-v1";
    private static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(5);

    public static WorkoutIntensityReport? Build(Guid ownerId, Workout workout, IReadOnlyList<WorkoutSeries> series,
        DateTimeOffset asOf, WorkoutIntensityProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(protocol);
        if (ownerId == Guid.Empty || ownerId != workout.OwnerId || workout.UpdatedAt > asOf ||
            workout.EndedAt > asOf || workout.EndedAt <= workout.StartedAt || !protocol.IsValid() ||
            series.Any(item => item.WorkoutId != workout.Id) ||
            series.Select(item => item.Metric).Distinct().Count() != series.Count)
        {
            return null;
        }

        var channel = series.SingleOrDefault(item => item.Metric == protocol.Metric);
        var samples = channel?.Samples().Select(item => new SignalSample(item.Time, item.Value)).ToArray() ?? [];
        var slices = Slices(samples, workout.StartedAt, workout.EndedAt, protocol.Metric);
        var distribution = AdvancedTrainingLoad.Distribute(slices, protocol.UpperBoundaries);
        if (distribution is null)
        {
            return null;
        }

        var observed = slices.Where(slice => slice.Intensity.HasValue).ToArray();
        var minutes = observed.Sum(slice => slice.Minutes);
        double? mean = minutes > 0
            ? observed.Sum(slice => slice.Minutes * slice.Intensity.GetValueOrDefault()) / minutes : null;
        if (mean is { } value && !double.IsFinite(value))
        {
            mean = null;
        }

        var heart = HeartLoad(protocol, observed, minutes, mean);
        var status = channel is null ? "missing-channel" : mean is null ? "insufficient-support" :
            distribution.MissingMinutes > 0 ? "partial-support" : "observed";
        return new WorkoutIntensityReport(workout.Id, workout.Revision, Convert.ToHexStringLower(workout.ContentHash),
            workout.Source, asOf, MethodVersion, MaxGap.TotalSeconds, protocol, status, protocol.Metric.Unit(),
            distribution, mean, heart.Reserve, heart.Trimp, heart.Status, AdvancedTrainingLoad.TrimpMethodVersion);
    }

    private static (double? Reserve, double? Trimp, string Status) HeartLoad(WorkoutIntensityProtocol protocol,
        IntensitySlice[] observed, double minutes, double? mean)
    {
        if (protocol.RestingBpm is not { } resting || protocol.MaximumBpm is not { } maximum)
        {
            return (null, null, "not-requested");
        }

        if (mean is not { } bpm)
        {
            return (null, null, "insufficient-support");
        }

        if (observed.Any(slice => slice.Intensity < resting || slice.Intensity > maximum))
        {
            return (null, null, "outside-calibration");
        }

        var reserve = TrainingLoadEngine.HeartRateReserveLoad(minutes, bpm, resting, maximum);
        var trimp = protocol.TrimpA is { } a && protocol.TrimpB is { } b
            ? AdvancedTrainingLoad.ExponentialTrimp(minutes, bpm, resting, maximum, a, b)
            : null;
        return (reserve, trimp, reserve is null ? "invalid-load" : "observed-support-only");
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<IntensitySlice> Slices(IReadOnlyList<SignalSample> samples,
        DateTimeOffset start, DateTimeOffset end, SeriesMetric metric)
    {
        var slots = samples.Where(sample => sample.Time >= start && sample.Time <= end)
            .GroupBy(sample => sample.Time).Select(SignalProcessing.Resolve).OrderBy(sample => sample.Time).ToArray();
        var slices = new List<IntensitySlice>();
        var cursor = start;
        for (var index = 0; index + 1 < slots.Length; index++)
        {
            var left = slots[index];
            var right = slots[index + 1];
            if (!Valid(left.Value, metric) || !Valid(right.Value, metric))
            {
                continue;
            }

            if (left.Time > cursor)
            {
                slices.Add(new IntensitySlice((left.Time - cursor).TotalMinutes, null));
            }

            var duration = right.Time - left.Time;
            var supported = duration <= MaxGap ? duration : MaxGap;
            slices.Add(new IntensitySlice(supported.TotalMinutes, left.Value));
            cursor = left.Time + supported;
        }

        if (cursor < end)
        {
            slices.Add(new IntensitySlice((end - cursor).TotalMinutes, null));
        }

        return slices.AsReadOnly();
    }

    private static bool Valid(double? value, SeriesMetric metric) =>
        value is { } number && double.IsFinite(number) && number >= 0 &&
        (metric != SeriesMetric.HeartRate || number > 0);
}
