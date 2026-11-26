using Microsoft.EntityFrameworkCore;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Ricostruisce revisioni del feedback e relativi valori, senza anticipare dati futuri.</summary>
public sealed class AthleteFeedbackQuery(MoscoRunDbContext db)
{
    public async Task<IReadOnlyList<AthleteFeedbackSnapshot>> ReadHistoryAsync(Guid ownerId, string entryId,
        DateTimeOffset asOf, CancellationToken ct = default)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Atleta obbligatorio.", nameof(ownerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        var cutoff = asOf.ToUniversalTime();
        var entries = await db.Set<AthleteFeedbackEntryRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId && x.EntryId == entryId && x.AvailableAt <= cutoff)
            .OrderBy(x => x.AvailableAt).ThenBy(x => x.Id).ToListAsync(ct);
        var ids = entries.SelectMany(x => x.ObservationIds).Distinct().ToArray();
        var values = await db.Set<ScientificObservationRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId && ids.Contains(x.Id) && x.AvailableAt <= cutoff)
            .ToDictionaryAsync(x => x.Id, ct);
        return Array.AsReadOnly(entries.Select(x => new AthleteFeedbackSnapshot(new AthleteFeedbackEntry(
            x.EntryId, x.Revision, x.Time, Array.AsReadOnly(x.ObservationIds.Select(id =>
            {
                var value = values[id];
                return new AthleteFeedbackValue(value.Metric, value.Unit, value.Value);
            }).OrderBy(v => v.Metric, StringComparer.Ordinal).ToArray()), x.WorkoutId, x.RevisionOrder, x.Note), x.AvailableAt)).ToArray());
    }
}
