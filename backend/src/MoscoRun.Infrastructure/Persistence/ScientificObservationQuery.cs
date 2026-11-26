using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificObservationPage(IReadOnlyList<ScientificObservation> Items, int? NextOffset);

/// <summary>Letture archiviate, non uno snapshot scientifico con lineage validato.</summary>
public sealed class ScientificObservationQuery(MoscoRunDbContext db)
{
    public const int MaxPageSize = 500;

    public async Task<ScientificObservationPage> ReadAsync(Guid ownerId, DateTimeOffset from, DateTimeOffset to,
        DateTimeOffset asOf, string? metric = null, int offset = 0, int limit = 100, CancellationToken ct = default)
    {
        if (ownerId == Guid.Empty || from >= to || offset < 0 || offset > int.MaxValue - MaxPageSize || limit is < 1 or > MaxPageSize)
        {
            throw new ArgumentException("Parametri di interrogazione non validi.");
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

        var rows = await query.OrderBy(o => o.Time).ThenBy(o => o.AvailableAt).ThenBy(o => o.Id)
            .Skip(offset).Take(limit + 1).ToListAsync(ct);
        var items = rows.Take(limit).Select(o => new ScientificObservation(o.Id, o.OwnerId, o.Metric, o.Unit,
            o.Value, o.Time, o.AvailableAt, o.Source, Enum.Parse<ObservationMethod>(o.Method),
            o.ProviderAlgorithm, o.DerivationVersion, o.InputIds)).ToArray();
        return new ScientificObservationPage(Array.AsReadOnly(items), rows.Count > limit ? offset + limit : null);
    }
}
