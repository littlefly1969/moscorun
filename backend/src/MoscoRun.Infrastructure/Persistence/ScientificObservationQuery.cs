using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Letture archiviate, non uno snapshot scientifico con lineage validato.</summary>
public sealed class ScientificObservationQuery(MoscoRunDbContext db)
{
    public const int MaxPageSize = 500;

    public async Task<ScientificObservationPage> ReadAsync(Guid ownerId, DateTimeOffset from, DateTimeOffset to,
        DateTimeOffset asOf, string? metric = null, ScientificObservationCursor? after = null, int limit = 100,
        CancellationToken ct = default)
    {
        if (ownerId == Guid.Empty || from >= to || limit is < 1 or > MaxPageSize)
        {
            throw new ArgumentException("Parametri di interrogazione non validi.", nameof(from));
        }

        var start = from.ToUniversalTime();
        var end = to.ToUniversalTime();
        var cutoff = asOf.ToUniversalTime();
        var query = db.Set<ScientificObservationRow>().AsNoTracking().Where(o =>
            o.OwnerId == ownerId && o.Time >= start && o.Time < end && o.AvailableAt <= cutoff);
        if (metric is not null)
        {
            query = query.Where(o => o.Metric == metric);
        }

        if (after is not null)
        {
            var time = after.Time.ToUniversalTime();
            var available = after.AvailableAt.ToUniversalTime();
            query = query.Where(o => o.Time > time || (o.Time == time &&
                (o.AvailableAt > available || (o.AvailableAt == available && o.Id.CompareTo(after.Id) > 0))));
        }

        var rows = await query.OrderBy(o => o.Time).ThenBy(o => o.AvailableAt).ThenBy(o => o.Id)
            .Take(limit + 1).ToListAsync(ct);
        var items = rows.Take(limit).Select(o => new ScientificObservation(o.Id, o.OwnerId, o.Metric, o.Unit,
            o.Value, o.Time, o.AvailableAt, o.Source, Enum.Parse<ObservationMethod>(o.Method),
            o.ProviderAlgorithm, o.DerivationVersion, o.InputIds)).ToArray();
        var last = items.LastOrDefault();
        var next = rows.Count > limit && last is not null ? new ScientificObservationCursor(last.Time, last.AvailableAt, last.Id) : null;
        return new ScientificObservationPage(Array.AsReadOnly(items), next);
    }
}
