using System.Data;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Legge una snapshot consistente di allenamenti e revisioni feedback per la finestra di 35 giorni.</summary>
public sealed class DailySessionLoadReader(MoscoRunDbContext db)
{
    private const int MaxSessions = 1000;
    private const int MaxFeedbackRevisions = 10000;

    public async Task<DailySessionLoadReport?> ReadAsync(Guid owner, DateOnly last, DateTimeOffset asOf,
        CancellationToken ct = default)
    {
        if (owner == Guid.Empty || !DailySessionLoadEngine.IsValidLastDay(last, asOf))
        {
            return null;
        }

        var cutoff = asOf.ToUniversalTime();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var result = await ReadCoreAsync(owner, last, cutoff, token);
            await transaction.CommitAsync(token);
            return result;
        }, ct);
    }

    private async Task<DailySessionLoadReport?> ReadCoreAsync(Guid owner, DateOnly last, DateTimeOffset asOf,
        CancellationToken ct)
    {
        var count = DailySessionLoadEngine.RecentDays + DailySessionLoadEngine.PriorDays;
        var days = Enumerable.Range(0, count).Select(index => last.AddDays(1 - count + index)).ToArray();
        var start = At(days[0]);
        var end = At(last).AddDays(1);
        var workouts = await db.Workouts.AsNoTracking().Where(workout => workout.OwnerId == owner &&
            workout.StartedAt < end && workout.EndedAt > start).OrderBy(workout => workout.StartedAt)
            .Take(MaxSessions + 1).ToListAsync(ct);
        if (workouts.Count > MaxSessions)
        {
            return null;
        }

        var keys = days.Select(ScientificFeedbackKeys.Daily).Concat(workouts.Select(workout =>
            ScientificFeedbackKeys.Session(workout.Id, workout.Revision))).ToArray();
        var rows = await db.Set<AthleteFeedbackEntryRow>().AsNoTracking().Where(row =>
                row.OwnerId == owner && keys.Contains(row.EntryId) && row.AvailableAt <= asOf)
            .Take(MaxFeedbackRevisions + 1).ToListAsync(ct);
        if (rows.Count > MaxFeedbackRevisions)
        {
            return null;
        }

        var latest = rows.GroupBy(row => row.EntryId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => Resolve(group.ToArray()), StringComparer.Ordinal);
        var ids = latest.Values.Where(value => value.Entry is not null)
            .SelectMany(value => value.Entry!.ObservationIds).Distinct().ToArray();
        var observations = await db.Set<ScientificObservationRow>().AsNoTracking().Where(row =>
                row.OwnerId == owner && ids.Contains(row.Id) && row.AvailableAt <= asOf &&
                (row.Metric == "rpe" || row.Metric == ScientificFeedbackKeys.DailyCompleteMetric))
            .ToDictionaryAsync(row => row.Id, ct);

        var attestations = days.Select(day => Attestation(day, latest.GetValueOrDefault(ScientificFeedbackKeys.Daily(day)),
            observations)).ToArray();
        var evidence = workouts.Select(workout => Session(workout,
            latest.GetValueOrDefault(ScientificFeedbackKeys.Session(workout.Id, workout.Revision)), observations)).ToArray();
        return DailySessionLoadEngine.Build(last, asOf, attestations, evidence);
    }

    private static FeedbackEvidence Resolve(AthleteFeedbackEntryRow[] rows) =>
        rows.Any(row => row.RevisionOrder is null) || rows.Select(row => row.RevisionOrder).Distinct().Count() != rows.Length
            ? new FeedbackEvidence(null, true)
            : new FeedbackEvidence(rows.MaxBy(row => row.RevisionOrder), false);

    private static DailyLoadAttestation Attestation(DateOnly day, FeedbackEvidence? evidence,
        Dictionary<Guid, ScientificObservationRow> observations)
    {
        var entry = evidence?.Entry;
        var value = entry is not null && entry.WorkoutId is null && entry.Time == At(day)
            ? Value(entry, ScientificFeedbackKeys.DailyCompleteMetric, "1", observations)
            : null;
        return new DailyLoadAttestation(day, value, entry?.AvailableAt, evidence?.Ambiguous == true);
    }

    private static SessionLoadEvidence Session(Workout workout, FeedbackEvidence? evidence,
        Dictionary<Guid, ScientificObservationRow> observations)
    {
        var entry = evidence?.Entry;
        var value = entry is not null && entry.WorkoutId == workout.Id && entry.Time == workout.EndedAt
            ? Value(entry, "rpe", "0-10", observations)
            : null;
        return new SessionLoadEvidence(workout.Id, workout.Revision, workout.StartedAt, workout.EndedAt,
            workout.UpdatedAt, workout.ActiveDuration?.TotalSeconds, value, entry?.AvailableAt, evidence?.Ambiguous == true);
    }

    private static double? Value(AthleteFeedbackEntryRow entry, string metric, string unit,
        Dictionary<Guid, ScientificObservationRow> observations)
    {
        var values = entry.ObservationIds.Where(observations.ContainsKey).Select(id => observations[id])
            .Where(row => string.Equals(row.Metric, metric, StringComparison.Ordinal) &&
                string.Equals(row.Unit, unit, StringComparison.Ordinal) &&
                string.Equals(row.Source, "athlete-feedback", StringComparison.Ordinal) &&
                string.Equals(row.Method, nameof(ObservationMethod.SelfReported), StringComparison.Ordinal) &&
                row.AvailableAt <= entry.AvailableAt && row.WorkoutId == entry.WorkoutId && row.Time == entry.Time)
            .ToArray();
        return values.Length == 1 ? values[0].Value : null;
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private sealed record FeedbackEvidence(AthleteFeedbackEntryRow? Entry, bool Ambiguous);
}
