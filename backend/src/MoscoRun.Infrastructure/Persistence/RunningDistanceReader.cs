using System.Data;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.Infrastructure.Persistence;

public sealed class RunningDistanceReader(MoscoRunDbContext db)
{
    public async Task<RunningDistanceReport?> ReadAsync(Guid owner, Guid id, DateTimeOffset asOf, CancellationToken ct = default)
    {
        if (owner == Guid.Empty || id == Guid.Empty)
        {
            return null;
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var report = await ReadCoreAsync(owner, id, asOf, token);
            await tx.CommitAsync(token);
            return report;
        }, ct);
    }

    private async Task<RunningDistanceReport?> ReadCoreAsync(Guid owner, Guid id, DateTimeOffset asOf, CancellationToken ct)
    {
        var current = await db.Workouts.AsNoTracking().SingleOrDefaultAsync(row => row.OwnerId == owner &&
            row.Id == id && row.EndedAt <= asOf && row.UpdatedAt <= asOf, ct);
        if (current is null)
        {
            return null;
        }

        var day = DateOnly.FromDateTime(current.StartedAt.UtcDateTime);
        if (day.DayNumber < RunningDistanceEngine.PriorDays)
        {
            return null;
        }

        var dates = Enumerable.Range(1, RunningDistanceEngine.PriorDays).Select(offset => day.AddDays(-offset)).ToArray();
        var start = At(dates[^1]);
        var end = At(day);
        var sessions = await db.Workouts.AsNoTracking().Where(row => row.OwnerId == owner && row.Id != id &&
            row.StartedAt < end && row.EndedAt > start).Take(1001).ToListAsync(ct);
        if (sessions.Count > 1000)
        {
            return null;
        }

        var attestations = await AttestationsAsync(owner, dates, current.StartedAt, ct);
        return attestations is null ? null : RunningDistanceEngine.Build(Evidence(current), attestations,
            sessions.Select(Evidence).ToArray(), asOf);
    }

    private async Task<DailyLoadAttestation[]?> AttestationsAsync(Guid owner, DateOnly[] dates, DateTimeOffset cutoff,
        CancellationToken ct)
    {
        var keys = dates.Select(ScientificFeedbackKeys.Daily).ToArray();
        var rows = await db.Set<AthleteFeedbackEntryRow>().AsNoTracking().Where(row =>
            row.OwnerId == owner && keys.Contains(row.EntryId) && row.AvailableAt <= cutoff)
            .Take(10001).ToListAsync(ct);
        if (rows.Count > 10000)
        {
            return null;
        }

        var groups = rows.GroupBy(row => row.EntryId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var latest = groups.Values.Where(group => !Ambiguous(group)).Select(group => group.MaxBy(row => row.RevisionOrder)!).ToArray();
        var ids = latest.SelectMany(row => row.ObservationIds).Distinct().ToArray();
        var observations = await db.Set<ScientificObservationRow>().AsNoTracking().Where(row =>
            row.OwnerId == owner && ids.Contains(row.Id) && row.AvailableAt <= cutoff &&
            row.Metric == ScientificFeedbackKeys.DailyCompleteMetric).ToDictionaryAsync(row => row.Id, ct);
        return dates.Select(day => Attestation(day, groups.GetValueOrDefault(ScientificFeedbackKeys.Daily(day)), observations))
            .ToArray();
    }

    private static bool Ambiguous(AthleteFeedbackEntryRow[] rows) => rows.Any(row => row.RevisionOrder is null) ||
        rows.Select(row => row.RevisionOrder).Distinct().Count() != rows.Length;

    private static DailyLoadAttestation Attestation(DateOnly day, AthleteFeedbackEntryRow[]? rows,
        Dictionary<Guid, ScientificObservationRow> observations)
    {
        var ambiguous = rows is not null && Ambiguous(rows);
        var entry = ambiguous ? null : rows?.MaxBy(row => row.RevisionOrder);
        double? value = null;
        if (entry is not null && entry.WorkoutId is null && entry.Time == At(day))
        {
            var matches = entry.ObservationIds.Where(observations.ContainsKey).Select(id => observations[id])
                .Where(row => row.WorkoutId is null && row.Time == entry.Time && row.AvailableAt <= entry.AvailableAt &&
                    string.Equals(row.Source, "athlete-feedback", StringComparison.Ordinal) &&
                    string.Equals(row.Method, nameof(ObservationMethod.SelfReported), StringComparison.Ordinal) &&
                    string.Equals(row.Unit, "1", StringComparison.Ordinal)).ToArray();
            value = matches.Length == 1 ? matches[0].Value : null;
        }

        return new DailyLoadAttestation(day, value, entry?.AvailableAt, ambiguous);
    }

    private static RunningDistanceEvidence Evidence(Workout workout) => new(workout.Id, workout.Revision,
        Convert.ToHexStringLower(workout.ContentHash), workout.Source, workout.ActivityType, workout.StartedAt,
        workout.EndedAt, workout.UpdatedAt, workout.DistanceM);

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
