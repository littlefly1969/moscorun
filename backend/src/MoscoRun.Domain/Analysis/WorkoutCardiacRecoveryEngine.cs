using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

public static class WorkoutCardiacRecoveryEngine
{
    public const string MethodVersion = "cardiac-recovery-native-causal-v1";
    private static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(5);
    private static readonly int[] Offsets = [0, 60, 120];
    private static readonly double[] Candidates = Enumerable.Range(1, 300).Select(value => (double)value).ToArray();

    public static WorkoutCardiacRecoveryReport? Build(Guid owner, Workout workout, IReadOnlyList<WorkoutSeries> series,
        DateTimeOffset asOf, WorkoutCardiacRecoveryProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(protocol);
        if (owner == Guid.Empty || workout.OwnerId != owner || workout.UpdatedAt > asOf || workout.EndedAt > asOf ||
            !protocol.IsValid() || protocol.EndOffsetSeconds > (workout.EndedAt - workout.StartedAt).TotalSeconds ||
            series.Any(item => item.WorkoutId != workout.Id) ||
            series.Select(item => item.Metric).Distinct().Count() != series.Count)
        {
            return null;
        }

        var end = workout.StartedAt.AddSeconds(protocol.EndOffsetSeconds);
        var channel = series.SingleOrDefault(item => item.Metric == SeriesMetric.HeartRate);
        var samples = protocol.Confirmed && channel is not null
            ? channel.Samples().Select(item => new SignalSample(item.Time, item.Value))
                .Where(item => item.Time >= workout.StartedAt && item.Time <= workout.EndedAt)
                .GroupBy(item => item.Time).Select(SignalProcessing.Resolve).OrderBy(item => item.Time).ToArray()
            : [];
        var initial = At(samples, end, workout.EndedAt);
        var points = Offsets.Select(offset => Point(samples, end, workout.EndedAt, initial is { } first ? first.Value : null, offset)).ToArray();
        var status = !protocol.Confirmed ? "unconfirmed-protocol" : channel is null ? "missing-channel" :
            initial is null ? "missing-end-heart-rate" : points.Skip(1).All(point => point.DropFromEndBpm.HasValue)
                ? "observed" : "partial-support";
        var (fit, fitStatus) = Fit(samples, end, workout.EndedAt, protocol.EquilibriumBpm);
        return new WorkoutCardiacRecoveryReport(workout.Id, workout.Revision, Convert.ToHexStringLower(workout.ContentHash),
            workout.Source, asOf, MethodVersion, MaximumAge.TotalSeconds, protocol, status,
            Array.AsReadOnly(points), fit, fitStatus);
    }

    private static SignalSample? At(SignalSample[] samples, DateTimeOffset target, DateTimeOffset workoutEnd)
    {
        if (target > workoutEnd)
        {
            return null;
        }

        for (var index = samples.Length - 1; index >= 0; index--)
        {
            var sample = samples[index];
            if (sample.Time <= target)
            {
                return target - sample.Time <= MaximumAge && sample.Value is > 0 ? sample : null;
            }
        }

        return null;
    }

    private static CardiacRecoveryPoint Point(SignalSample[] samples, DateTimeOffset end,
        DateTimeOffset workoutEnd, double? initial, int offset)
    {
        var target = end.AddSeconds(offset);
        var (time, value) = At(samples, target, workoutEnd) is { } sample
            ? ((DateTimeOffset?)sample.Time, sample.Value) : (null, null);
        var drop = offset > 0 && initial is { } start && value is { } bpm
            ? CardiacDynamics.HeartRateRecovery(start, bpm) : null;
        return new CardiacRecoveryPoint(offset, target, time, value, drop);
    }

    private static (CardiacResponseFit? Fit, string Status) Fit(SignalSample[] samples, DateTimeOffset end,
        DateTimeOffset workoutEnd, double? equilibrium)
    {
        if (equilibrium is not { } baseline)
        {
            return (null, "not-requested");
        }

        var until = end.AddSeconds(120);
        var rows = samples.Where(sample => sample.Time >= end && sample.Time <= until).ToArray();
        if (until > workoutEnd || rows.Length < 5 || rows[0].Time != end || rows[^1].Time != until ||
            rows.Any(sample => sample.Value is not > 0) || rows[0].Value <= baseline)
        {
            return (null, "insufficient-fit-support");
        }

        for (var index = 1; index < rows.Length; index++)
        {
            if (rows[index].Time - rows[index - 1].Time > MaximumAge)
            {
                return (null, "insufficient-fit-support");
            }
        }

        var points = rows.Skip(1).Select(sample =>
            new CardiacResponsePoint((sample.Time - end).TotalSeconds, sample.Value.GetValueOrDefault())).ToArray();
        var fit = CardiacResponseCalibration.Fit(points, rows[0].Value.GetValueOrDefault(), baseline, Candidates);
        return (fit, fit is null ? "not-identifiable" : "descriptive-fit");
    }
}
