using System.Data;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed class SleepDiaryReader(MoscoRunDbContext db)
{
    public async Task<SleepDiaryReport?> ReadAsync(Guid owner, DateOnly last, int days, DateTimeOffset asOf,
        CancellationToken ct = default)
    {
        if (owner == Guid.Empty || !SleepDiaryEngine.ValidWindow(last, days, asOf))
        {
            return null;
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var result = await ReadCoreAsync(owner, last, days, asOf, token);
            await tx.CommitAsync(token);
            return result;
        }, ct);
    }

    private async Task<SleepDiaryReport?> ReadCoreAsync(Guid owner, DateOnly last, int days, DateTimeOffset asOf,
        CancellationToken ct)
    {
        var dates = Enumerable.Range(0, days).Select(index => last.AddDays(index + 1 - days)).ToArray();
        var keys = dates.Select(ScientificFeedbackKeys.Daily).ToArray();
        var rows = await db.Set<AthleteFeedbackEntryRow>().AsNoTracking().Where(row =>
            row.OwnerId == owner && keys.Contains(row.EntryId) && row.AvailableAt <= asOf)
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
            row.OwnerId == owner && ids.Contains(row.Id) && row.AvailableAt <= asOf &&
            (row.Metric == "sleepDuration" || row.Metric == SleepDiaryMetrics.Bedtime ||
             row.Metric == SleepDiaryMetrics.Wake || row.Metric == SleepDiaryMetrics.InBed ||
             row.Metric == SleepDiaryMetrics.Confirmed)).ToDictionaryAsync(row => row.Id, ct);
        var nights = dates.Select(day => Night(day, groups.GetValueOrDefault(ScientificFeedbackKeys.Daily(day)), observations)).ToArray();
        return SleepDiaryEngine.Build(last, days, asOf, nights);
    }

    private static bool Ambiguous(AthleteFeedbackEntryRow[] rows) => rows.Any(row => row.RevisionOrder is null) ||
        rows.Select(row => row.RevisionOrder).Distinct().Count() != rows.Length;

    private static SleepDiaryNight Night(DateOnly day, AthleteFeedbackEntryRow[]? rows,
        Dictionary<Guid, ScientificObservationRow> observations)
    {
        var values = new Dictionary<string, double?>(StringComparer.Ordinal);
        var ambiguous = rows is not null && Ambiguous(rows);
        var entry = ambiguous ? null : rows?.MaxBy(row => row.RevisionOrder);
        if (entry is not null && entry.WorkoutId is null &&
            entry.Time == new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))
        {
            var eligible = entry.ObservationIds.Where(observations.ContainsKey).Select(id => observations[id])
                .Where(row => row.WorkoutId is null && row.Time == entry.Time && row.AvailableAt <= entry.AvailableAt &&
                    string.Equals(row.Source, "athlete-feedback", StringComparison.Ordinal) &&
                    string.Equals(row.Method, nameof(ObservationMethod.SelfReported), StringComparison.Ordinal) &&
                    ScientificMetricCatalog.AcceptsUnit(row.Metric, row.Unit));
            foreach (var group in eligible.GroupBy(row => row.Metric, StringComparer.Ordinal))
            {
                values[group.Key] = group.Count() == 1 ? group.Single().Value : null;
            }
        }

        return SleepDiaryEngine.Night(day, entry?.RevisionOrder, entry?.AvailableAt, ambiguous, values);
    }
}
