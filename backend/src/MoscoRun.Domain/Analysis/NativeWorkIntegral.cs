using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

/// <summary>Integrale ZOH soltanto su supporto completo. Nessun riempimento di lacune o estrapolazione.</summary>
public static class NativeWorkIntegral
{
    public const double MaximumGapSeconds = 5;

    public static double? MeanPower(Workout workout, WorkoutSeries series)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(series);
        if (series.WorkoutId != workout.Id || series.Metric != SeriesMetric.Power)
        {
            return null;
        }

        var slots = series.Samples().Select(item => new SignalSample(item.Time, item.Value))
            .Where(item => item.Time >= workout.StartedAt && item.Time <= workout.EndedAt)
            .GroupBy(item => item.Time).Select(SignalProcessing.Resolve).OrderBy(item => item.Time).ToArray();
        if (slots.Length < 2 || slots[0].Time != workout.StartedAt || slots[^1].Time != workout.EndedAt ||
            slots.Any(item => item.Value is not { } value || !double.IsFinite(value) || value < 0))
        {
            return null;
        }

        double work = 0;
        for (var index = 1; index < slots.Length; index++)
        {
            var seconds = (slots[index].Time - slots[index - 1].Time).TotalSeconds;
            if (seconds <= 0 || seconds > MaximumGapSeconds)
            {
                return null;
            }

            work += seconds * slots[index - 1].Value.GetValueOrDefault();
        }

        var mean = work / (workout.EndedAt - workout.StartedAt).TotalSeconds;
        return double.IsFinite(mean) && mean > 0 ? mean : null;
    }
}
