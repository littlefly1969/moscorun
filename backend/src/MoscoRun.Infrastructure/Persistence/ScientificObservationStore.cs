using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Archivio append-only delle osservazioni, con isolamento per atleta e lettura point-in-time.</summary>
public sealed class ScientificObservationStore(MoscoRunDbContext db)
{
    /// <summary>Inserisce una nuova osservazione. Il chiamante deve fornire l'identità autorizzata.</summary>
    public async Task AppendAsync(Guid ownerId, ScientificObservation observation, CancellationToken ct = default)
    {
        _ = await ScientificWriteGate.RunAsync(db, ownerId, async token =>
        {
            await AppendCoreAsync(ownerId, observation, token);
            return true;
        }, ct);
    }

    private async Task AppendCoreAsync(Guid ownerId, ScientificObservation observation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ValidateOwner(ownerId);
        if (observation.OwnerId != ownerId)
        {
            throw new ArgumentException("Osservazione di un altro atleta.", nameof(observation));
        }

        var inputs = observation.InputIds.ToArray();
        if (inputs.Length > 0)
        {
            var count = await db.Set<ScientificObservationRow>()
                .CountAsync(o => o.OwnerId == ownerId && inputs.Contains(o.Id) && o.AvailableAt <= observation.AvailableAt, ct);
            if (count != inputs.Length)
            {
                throw new ArgumentException("Ingressi mancanti, futuri o di un altro atleta.", nameof(observation));
            }
        }

        db.Set<ScientificObservationRow>().Add(new ScientificObservationRow
        {
            OwnerId = ownerId,
            Id = observation.Id,
            Metric = observation.Metric,
            Unit = observation.Unit,
            Value = observation.Value,
            Time = observation.Time,
            AvailableAt = observation.AvailableAt,
            Source = observation.Source,
            Method = observation.Method.ToString(),
            ProviderAlgorithm = observation.ProviderAlgorithm,
            DerivationVersion = observation.DerivationVersion,
            InputIds = inputs,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Restituisce le osservazioni conoscibili all'istante richiesto, con lineage valido.</summary>
    public async Task<IReadOnlyList<ScientificObservation>> ReadAsOfAsync(Guid ownerId, DateTimeOffset asOf, CancellationToken ct = default)
    {
        ValidateOwner(ownerId);
        var cutoff = asOf.ToUniversalTime();
        var rows = await db.Set<ScientificObservationRow>().AsNoTracking()
            .Where(o => o.OwnerId == ownerId && o.Time <= cutoff && o.AvailableAt <= cutoff)
            .ToListAsync(ct);
        return ScientificSnapshot.Select(ownerId, cutoff, rows.Select(o => new ScientificObservation(
            o.Id, o.OwnerId, o.Metric, o.Unit, o.Value, o.Time, o.AvailableAt, o.Source,
            Enum.Parse<ObservationMethod>(o.Method), o.ProviderAlgorithm, o.DerivationVersion, o.InputIds)));
    }

    private static void ValidateOwner(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Atleta obbligatorio.", nameof(ownerId));
        }
    }
}
