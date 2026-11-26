using System.Data;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.Infrastructure.Persistence;

public sealed class AttestedRaceReader(MoscoRunDbContext db)
{
    private const int MaxWorkouts = 2048;
    private const int MaxRevisions = 20000;

    public async Task<AttestedRaceReport?> ReadAsync(Guid owner, string source, DateTimeOffset start,
        DateTimeOffset split, DateTimeOffset end, DateTimeOffset asOf, double target, CancellationToken ct = default)
    {
        if (owner == Guid.Empty || string.IsNullOrWhiteSpace(source) || source.Length > Workout.SourceMaxLength ||
            !AttestedRaceEngine.ValidWindow(start, split, end, asOf, target))
        {
            return null;
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var result = await ReadCoreAsync(owner, source, start.ToUniversalTime(), split.ToUniversalTime(),
                end.ToUniversalTime(), asOf.ToUniversalTime(), target, token);
            await transaction.CommitAsync(token);
            return result;
        }, ct);
    }

    private async Task<AttestedRaceReport?> ReadCoreAsync(Guid owner, string source, DateTimeOffset start,
        DateTimeOffset split, DateTimeOffset end, DateTimeOffset asOf, double target, CancellationToken ct)
    {
        var workouts = await db.Workouts.AsNoTracking().Where(workout => workout.OwnerId == owner &&
            workout.Source == source && workout.StartedAt >= start && workout.EndedAt < end &&
            workout.UpdatedAt <= asOf && workout.DistanceM >= 100 && workout.DistanceM <= 100000)
            .OrderBy(workout => workout.StartedAt).Take(MaxWorkouts + 1).ToListAsync(ct);
        if (workouts.Count > MaxWorkouts)
        {
            return null;
        }

        var runs = workouts.Where(workout => string.Equals(workout.ActivityType, "RUNNING", StringComparison.OrdinalIgnoreCase)).ToArray();
        var keys = runs.Select(workout => ScientificFeedbackKeys.Session(workout.Id, workout.Revision)).ToArray();
        var rows = await db.Set<AthleteFeedbackEntryRow>().AsNoTracking().Where(row =>
                row.OwnerId == owner && keys.Contains(row.EntryId) && row.AvailableAt <= asOf)
            .Take(MaxRevisions + 1).ToListAsync(ct);
        if (rows.Count > MaxRevisions)
        {
            return null;
        }

        var latest = rows.GroupBy(row => row.EntryId, StringComparer.Ordinal).Where(group =>
            group.All(row => row.RevisionOrder.HasValue) &&
            group.Select(row => row.RevisionOrder).Distinct().Count() == group.Count())
            .ToDictionary(group => group.Key, group => group.MaxBy(row => row.RevisionOrder)!, StringComparer.Ordinal);
        var ids = latest.Values.SelectMany(row => row.ObservationIds).Distinct().ToArray();
        var attestations = await db.Set<ScientificObservationRow>().AsNoTracking().Where(row =>
            row.OwnerId == owner && ids.Contains(row.Id) && (row.Metric == AttestedRaceEngine.AttestationMetric || row.Metric == AttestedPerformanceEngine.PowerAttestation) &&
            row.AvailableAt <= asOf).ToDictionaryAsync(row => row.Id, ct);
        var evidence = runs.Select(workout => Evidence(workout,
            latest.GetValueOrDefault(ScientificFeedbackKeys.Session(workout.Id, workout.Revision)), attestations))
            .OfType<AttestedRaceEffort>().ToArray();
        evidence = await AttachPowerAsync(runs, evidence, latest, attestations, split, ct);
        return AttestedRaceEngine.Build(owner, source, start, split, end, asOf, target, evidence);
    }

    private async Task<AttestedRaceEffort[]> AttachPowerAsync(Workout[] workouts, AttestedRaceEffort[] evidence,
        Dictionary<string, AthleteFeedbackEntryRow> latest, Dictionary<Guid, ScientificObservationRow> attestations,
        DateTimeOffset split, CancellationToken ct)
    {
        var eligible = evidence.Where(item => item.EndedAt < split && item.AvailableAt <= split &&
            (item.EndedAt - item.StartedAt).TotalSeconds is >= PerformancePhysiology.MinimumDurationSeconds and <= PerformancePhysiology.MaximumDurationSeconds &&
            PowerConfirmed(latest[ScientificFeedbackKeys.Session(item.WorkoutId, item.Revision)], attestations)).ToArray();
        if (eligible.Length is 0 or > 128)
        {
            return evidence;
        }

        var ids = eligible.Select(item => item.WorkoutId).ToArray();
        var byId = workouts.ToDictionary(item => item.Id);
        var power = new Dictionary<Guid, double?>();
        foreach (var id in ids)
        {
            var series = await db.WorkoutSeries.AsNoTracking().SingleOrDefaultAsync(item =>
                item.WorkoutId == id && item.Metric == SeriesMetric.Power, ct);
            power[id] = series is null ? null : NativeWorkIntegral.MeanPower(byId[id], series);
        }

        return evidence.Select(item => item with { MeanPowerWatts = power.GetValueOrDefault(item.WorkoutId) }).ToArray();
    }

    private static bool PowerConfirmed(AthleteFeedbackEntryRow entry, Dictionary<Guid, ScientificObservationRow> observations)
    {
        var values = entry.ObservationIds.Where(observations.ContainsKey).Select(id => observations[id])
            .Where(row => string.Equals(row.Metric, AttestedPerformanceEngine.PowerAttestation, StringComparison.Ordinal) &&
                row.WorkoutId == entry.WorkoutId && row.Time == entry.Time && row.AvailableAt <= entry.AvailableAt &&
                string.Equals(row.Unit, "1", StringComparison.Ordinal) &&
                string.Equals(row.Source, "athlete-feedback", StringComparison.Ordinal) &&
                string.Equals(row.Method, nameof(ObservationMethod.SelfReported), StringComparison.Ordinal)).ToArray();
        return values.Length == 1 && values[0].Value == 1;
    }

    private static AttestedRaceEffort? Evidence(Workout workout, AthleteFeedbackEntryRow? entry,
        Dictionary<Guid, ScientificObservationRow> observations)
    {
        if (entry is null || entry.WorkoutId != workout.Id || entry.Time != workout.EndedAt ||
            entry.AvailableAt < workout.UpdatedAt || entry.AvailableAt < workout.EndedAt || entry.RevisionOrder is not > 0)
        {
            return null;
        }

        var values = entry.ObservationIds.Where(observations.ContainsKey).Select(id => observations[id])
            .Where(row => string.Equals(row.Metric, AttestedRaceEngine.AttestationMetric, StringComparison.Ordinal) &&
                row.WorkoutId == workout.Id && row.Time == entry.Time &&
                row.AvailableAt <= entry.AvailableAt &&
                string.Equals(row.Unit, "1", StringComparison.Ordinal) &&
                string.Equals(row.Source, "athlete-feedback", StringComparison.Ordinal) &&
                string.Equals(row.Method, nameof(ObservationMethod.SelfReported), StringComparison.Ordinal)).ToArray();
        if (values.Length != 1 || values[0].Value != 1)
        {
            return null;
        }

        return new AttestedRaceEffort(workout.OwnerId, workout.Id, workout.Revision,
            Convert.ToHexStringLower(workout.ContentHash), workout.Source, workout.StartedAt, workout.EndedAt,
            entry.AvailableAt, entry.RevisionOrder.Value, workout.DistanceM.GetValueOrDefault());
    }
}
