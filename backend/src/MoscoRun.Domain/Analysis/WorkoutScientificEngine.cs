using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

/// <summary>Audit della revisione corrente delle serie originali, senza inferire canali non disponibili.</summary>
public static class WorkoutScientificEngine
{
    public const string MethodVersion = "workout-channel-quality-v1";
    private static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(5);

    public static WorkoutScienceReport? Build(
        Guid ownerId, Workout workout, IReadOnlyList<WorkoutSeries> series, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(series);
        if (ownerId == Guid.Empty || workout.OwnerId != ownerId || workout.UpdatedAt > asOf ||
            workout.EndedAt > asOf || workout.EndedAt <= workout.StartedAt ||
            series.Any(item => item.WorkoutId != workout.Id || !Enum.IsDefined(item.Metric)) ||
            series.Select(item => item.Metric).Distinct().Count() != series.Count)
        {
            return null;
        }

        var byMetric = series.ToDictionary(item => item.Metric);
        var channels = new List<WorkoutChannelReport>();
        foreach (var metric in Enum.GetValues<SeriesMetric>())
        {
            if (!byMetric.TryGetValue(metric, out var recorded))
            {
                channels.Add(new WorkoutChannelReport(metric, metric.Unit(), "missing", 0, 0, 0, 0, null));
                continue;
            }

            var quality = SignalProcessing.Audit(recorded.Samples().Select(item => new SignalSample(item.Time, item.Value)),
                workout.StartedAt, workout.EndedAt, MaxGap);
            var status = quality.TimeWeightedMean.HasValue ? "observed" : "insufficient-support";
            channels.Add(new WorkoutChannelReport(metric, metric.Unit(), status, quality.SampleCount,
                quality.ValidCount, quality.ConflictingCount, quality.Coverage, quality.TimeWeightedMean));
        }

        return new WorkoutScienceReport(workout.Id, workout.Revision, Convert.ToHexStringLower(workout.ContentHash),
            workout.Source, asOf, MethodVersion, MaxGap.TotalSeconds, channels.AsReadOnly());
    }
}
