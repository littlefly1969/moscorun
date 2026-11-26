using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

/// <summary>Replay condizionale su prefisso nativo continuo; nessuna ripresa dello stato oltre una lacuna.</summary>
public static class WorkoutReserveEngine
{
    public const string MethodVersion = "native-critical-reserve-prefix-v1";
    public const double MaximumGapSeconds = 5;
    private const int MaximumPoints = 300;

    public static WorkoutReserveReport? Build(Guid owner, Workout workout, IReadOnlyList<WorkoutSeries> series,
        DateTimeOffset asOf, WorkoutReserveProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(protocol);
        if (owner == Guid.Empty || workout.OwnerId != owner || workout.UpdatedAt > asOf || workout.EndedAt > asOf ||
            !protocol.IsValid() || workout.EndedAt <= workout.StartedAt ||
            series.Any(item => item.WorkoutId != workout.Id) ||
            series.Select(item => item.Metric).Distinct().Count() != series.Count)
        {
            return null;
        }

        var channel = series.SingleOrDefault(item => item.Metric == protocol.Metric);
        var segments = channel is null ? [] : Prefix(workout, channel, protocol.RecoveryTimeConstantSeconds);
        var duration = (workout.EndedAt - workout.StartedAt).TotalSeconds;
        var supported = segments.Sum(item => item.DurationSeconds);
        var states = CriticalReserveEngine.Replay(segments, protocol.CriticalIntensity, protocol.Capacity, protocol.InitialRemaining);
        var status = channel is null ? "missing-channel" : states is null ? "insufficient-prefix" :
            states[^1].Exhausted ? "conditional-exhaustion" : supported < duration - 1e-6 ? "truncated-at-gap" : "observed-prefix";
        return Report(workout, asOf, protocol, duration, supported, status, segments, states);
    }

    private static WorkoutReserveReport Report(Workout workout, DateTimeOffset asOf, WorkoutReserveProtocol protocol,
        double duration, double supported, string status, ReserveSegment[] segments, IReadOnlyList<ReserveState>? states)
    {
        double? exhaustion = null;
        if (states is not null && states[^1].Exhausted)
        {
            var index = states.Count - 1;
            var previous = index == 0 ? protocol.InitialRemaining : states[index - 1].Remaining;
            var elapsed = index == 0 ? 0 : states[index - 1].CumulativeSeconds;
            var excess = segments[index].Intensity - protocol.CriticalIntensity;
            if (excess > 0)
            {
                exhaustion = elapsed + previous / excess;
            }
        }

        return new WorkoutReserveReport(workout.Id, workout.Revision, Convert.ToHexStringLower(workout.ContentHash),
            workout.Source, asOf, MethodVersion, protocol, protocol.Metric == SeriesMetric.Power ? "J" : "m",
            status, duration, supported, exhaustion ?? states?[^1].CumulativeSeconds ?? 0, states?.Count ?? 0,
            states is null ? null : Math.Min(protocol.InitialRemaining, states.Min(item => item.Remaining)),
            states?[^1].Remaining, exhaustion, Points(states, protocol.InitialRemaining, exhaustion));
    }

    private static ReserveSegment[] Prefix(Workout workout, WorkoutSeries channel, double tau)
    {
        var slots = channel.Samples().Select(item => new SignalSample(item.Time, item.Value))
            .Where(item => item.Time >= workout.StartedAt && item.Time <= workout.EndedAt)
            .GroupBy(item => item.Time).Select(SignalProcessing.Resolve).OrderBy(item => item.Time).ToArray();
        if (slots.Length < 2 || slots[0].Time != workout.StartedAt)
        {
            return [];
        }

        var result = new List<ReserveSegment>();
        for (var index = 1; index < slots.Length; index++)
        {
            var seconds = (slots[index].Time - slots[index - 1].Time).TotalSeconds;
            if (seconds <= 0 || seconds > MaximumGapSeconds ||
                slots[index - 1].Value is not >= 0 || slots[index].Value is not >= 0)
            {
                break;
            }

            result.Add(new ReserveSegment(seconds, slots[index - 1].Value.GetValueOrDefault(), tau));
        }

        return result.ToArray();
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<ReserveState> Points(
        IReadOnlyList<ReserveState>? states, double initial, double? exhaustion)
    {
        if (states is null)
        {
            return Array.AsReadOnly(Array.Empty<ReserveState>());
        }

        var result = new List<ReserveState> { new(0, initial, false) };
        var step = (int)Math.Ceiling(states.Count / (double)(MaximumPoints - 2));
        for (var index = step - 1; index < states.Count - 1; index += step)
        {
            result.Add(states[index]);
        }

        result.Add(exhaustion is { } end ? states[^1] with { CumulativeSeconds = end } : states[^1]);
        return result.AsReadOnly();
    }
}
