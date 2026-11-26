using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

/// <summary>Applica un protocollo esplicito alle serie native della revisione autorizzata.</summary>
public static class WorkoutDurabilityEngine
{
    private static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(5);

    public static WorkoutDurabilityReport? Build(
        Guid ownerId, Workout workout, IReadOnlyList<WorkoutSeries> series, DateTimeOffset asOf,
        WorkoutDurabilityProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(protocol);
        if (ownerId == Guid.Empty || ownerId != workout.OwnerId || workout.UpdatedAt > asOf ||
            workout.EndedAt > asOf || !protocol.Fits(workout.EndedAt - workout.StartedAt) ||
            series.Any(item => item.WorkoutId != workout.Id) ||
            series.Select(item => item.Metric).Distinct().Count() != series.Count)
        {
            return null;
        }

        var loadSeries = series.SingleOrDefault(item => item.Metric == protocol.Metric);
        var heartSeries = series.SingleOrDefault(item => item.Metric == SeriesMetric.HeartRate);
        if (loadSeries is null || heartSeries is null)
        {
            return Report(workout, asOf, protocol, "missing-channel", [], null);
        }

        var load = loadSeries.Samples().Select(item => new SignalSample(item.Time, item.Value)).ToArray();
        var heart = heartSeries.Samples().Select(item => new SignalSample(item.Time, item.Value)).ToArray();
        var metric = protocol.Metric == SeriesMetric.Speed ? ExternalLoadMetric.Speed : ExternalLoadMetric.Power;
        var windows = new List<DurabilityWindow>(protocol.WindowCount);
        var reports = new List<WorkoutDurabilityWindow>(protocol.WindowCount);
        for (var index = 0; index < protocol.WindowCount; index++)
        {
            var offset = protocol.StartOffsetSeconds + index * protocol.WindowSeconds;
            var start = workout.StartedAt.AddSeconds(offset);
            var end = start.AddSeconds(protocol.WindowSeconds);
            var lower = start - MaxGap;
            var paired = PairedDurabilityEngine.BuildWindow(workout.Id, metric,
                load.Where(item => item.Time >= lower && item.Time <= end),
                heart.Where(item => item.Time >= lower && item.Time <= end), start, end, MaxGap);
            if (paired is { } window)
            {
                windows.Add(window);
                reports.Add(new WorkoutDurabilityWindow(offset, window.PairedDuration.TotalSeconds,
                    window.PairedDuration.TotalSeconds / protocol.WindowSeconds, window.MeanExternalLoad,
                    window.MeanHeartRateBpm));
            }
            else
            {
                reports.Add(new WorkoutDurabilityWindow(offset, 0, 0, null, null));
            }
        }

        var profile = windows.Count == protocol.WindowCount
            ? DurabilityProfileEngine.Analyze(windows, protocol.MinimumCoverage, protocol.OnsetLossPercent,
                protocol.MinimumConsecutiveWindows)
            : null;
        return Report(workout, asOf, protocol, profile is null ? "insufficient-support" : "observed",
            reports.AsReadOnly(), profile);
    }

    private static WorkoutDurabilityReport Report(
        Workout workout, DateTimeOffset asOf, WorkoutDurabilityProtocol protocol, string status,
        IReadOnlyList<WorkoutDurabilityWindow> windows, DurabilityProfile? profile) =>
        new(workout.Id, workout.Revision, Convert.ToHexStringLower(workout.ContentHash), workout.Source, asOf,
            DurabilityProfileEngine.MethodVersion, PairedDurabilityEngine.MethodVersion, MaxGap.TotalSeconds,
            protocol, status, windows, profile);
}
