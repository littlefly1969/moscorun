using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Lettura limitata, point-in-time, con risoluzione delle revisioni ordinate.</summary>
public sealed class ScientificDailyMetricReader(MoscoRunDbContext db)
{
    private const int MaxCandidates = 10000;

    public async Task<DailyMetricReport?> ReadAsync(Guid ownerId, string metric, string source,
        DateOnly day, int baselineDays, DateTimeOffset asOf, CancellationToken ct = default)
    {
        if (ownerId == Guid.Empty || !DailyMetricEngine.IsSupported(metric) ||
            string.IsNullOrWhiteSpace(source) || source.Length > 200 ||
            !DailyMetricEngine.IsValidWindow(day, baselineDays, asOf))
        {
            return null;
        }

        var from = new DateTimeOffset(day.AddDays(-baselineDays).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(day.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var cutoff = asOf.ToUniversalTime();
        var all = db.Set<ScientificObservationRow>().AsNoTracking().Where(row =>
            row.OwnerId == ownerId && row.Metric == metric && row.Source == source &&
            row.AvailableAt <= cutoff && row.Time <= cutoff);
        var rows = await all.Where(row => row.Time >= from && row.Time < to &&
                (row.ExternalId == null || !all.Any(other => other.ExternalId == row.ExternalId &&
                    other.ExternalRevisionOrder > row.ExternalRevisionOrder)))
            .Select(row => new
            {
                Row = row,
                Ambiguous = row.ExternalId != null && all.Any(other => other.ExternalId == row.ExternalId &&
                    other.Id != row.Id && (other.ExternalRevisionOrder == null || row.ExternalRevisionOrder == null ||
                        other.ExternalRevisionOrder == row.ExternalRevisionOrder)),
            }).OrderBy(item => item.Row.Time).ThenBy(item => item.Row.Id).Take(MaxCandidates + 1).ToListAsync(ct);
        if (rows.Count > MaxCandidates)
        {
            return null;
        }

        var candidates = rows.Select(item => new DailyMetricCandidate(ToObservation(item.Row), item.Ambiguous));
        return DailyMetricEngine.Build(ownerId, metric, source, day, baselineDays, cutoff, candidates);
    }

    private static ScientificObservation ToObservation(ScientificObservationRow row) =>
        new(row.Id, row.OwnerId, row.Metric, row.Unit, row.Value, row.Time, row.AvailableAt, row.Source,
            Enum.Parse<ObservationMethod>(row.Method), row.ProviderAlgorithm, row.DerivationVersion, row.InputIds);
}
