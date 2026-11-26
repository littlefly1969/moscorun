using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Operazioni server riservate; il chiamante deve autorizzare l'atleta e registrare l'audit.</summary>
public sealed class ScientificDataLifecycle(MoscoRunDbContext db)
{
    public async Task<int> CountBeforeAsync(Guid ownerId, DateTimeOffset cutoff, CancellationToken ct = default)
    {
        Validate(ownerId);
        var utc = cutoff.ToUniversalTime();
        return await db.Set<ScientificObservationRow>().CountAsync(o => o.OwnerId == ownerId && o.AvailableAt < utc, ct);
    }

    /// <summary>Esportazione delle osservazioni archiviate, incluse quelle con lineage incompleto.</summary>
    public async Task<IReadOnlyList<ScientificObservation>> ExportAsync(Guid ownerId, CancellationToken ct = default)
    {
        Validate(ownerId);
        var rows = await db.Set<ScientificObservationRow>().AsNoTracking().Where(o => o.OwnerId == ownerId)
            .OrderBy(o => o.Time).ThenBy(o => o.AvailableAt).ThenBy(o => o.Id).ToListAsync(ct);
        return Array.AsReadOnly(rows.Select(o => new ScientificObservation(o.Id, o.OwnerId, o.Metric, o.Unit,
            o.Value, o.Time, o.AvailableAt, o.Source, Enum.Parse<ObservationMethod>(o.Method),
            o.ProviderAlgorithm, o.DerivationVersion, o.InputIds)).ToArray());
    }

    /// <summary>Elimina tutte le osservazioni di un solo atleta; da usare dopo conferma e blocco dell'ingestione.</summary>
    public async Task<int> EraseAllAsync(Guid ownerId, CancellationToken ct = default)
    {
        Validate(ownerId);
        if (db.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Usare uno scope dedicato.");
        }

        return await db.Set<ScientificObservationRow>().Where(o => o.OwnerId == ownerId).ExecuteDeleteAsync(ct);
    }

    private static void Validate(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Atleta obbligatorio.", nameof(ownerId));
        }
    }
}
