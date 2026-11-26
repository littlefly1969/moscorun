using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed class ScientificRevisionQuery(MoscoRunDbContext db)
{
    public async Task<IReadOnlyList<ScientificImportedRevision>> ReadHistoryAsync(Guid ownerId, string source,
        string externalId, string metric, DateTimeOffset asOf, CancellationToken ct = default)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Atleta obbligatorio.", nameof(ownerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(metric);
        var cutoff = asOf.ToUniversalTime();
        var rows = await db.Set<ScientificObservationRow>().AsNoTracking().Where(o => o.OwnerId == ownerId &&
            o.Source == source && o.ExternalId == externalId && o.Metric == metric && o.AvailableAt <= cutoff)
            .OrderBy(o => o.AvailableAt).ThenBy(o => o.Id).ToListAsync(ct);
        return Array.AsReadOnly(rows.Select(o => new ScientificImportedRevision(o.ExternalRevision!, o.ExternalRevisionOrder,
            new ScientificObservation(o.Id, o.OwnerId, o.Metric, o.Unit, o.Value, o.Time, o.AvailableAt, o.Source,
                Enum.Parse<ObservationMethod>(o.Method), o.ProviderAlgorithm, o.DerivationVersion, o.InputIds))).ToArray());
    }

    public async Task<ScientificImportedRevision?> ReadLatestOrderedAsync(Guid ownerId, string source, string externalId,
        string metric, DateTimeOffset asOf, CancellationToken ct = default)
    {
        var revisions = await ReadHistoryAsync(ownerId, source, externalId, metric, asOf, ct);
        if (revisions.Count == 0)
        {
            return null;
        }

        if (revisions.Any(x => x.RevisionOrder is null) ||
            revisions.Select(x => x.RevisionOrder).Distinct().Count() != revisions.Count)
        {
            throw new InvalidOperationException("Il provider non ha fornito un ordinamento univoco delle revisioni.");
        }

        return revisions.MaxBy(x => x.RevisionOrder);
    }
}
