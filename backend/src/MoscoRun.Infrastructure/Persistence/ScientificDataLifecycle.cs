using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Operazioni riservate alla chiamata server autorizzata. L'audit non contiene misure o note.</summary>
public sealed class ScientificDataLifecycle(MoscoRunDbContext db, TimeProvider clock)
{
    public Task<ScientificLifecycleCounts> PreviewRetentionAsync(Guid ownerId, DateTimeOffset cutoff, CancellationToken ct = default)
    {
        var utc = ValidateCutoff(cutoff);
        return LockedAsync(ownerId, token => new ScientificRetention(db).PreviewAsync(ownerId, utc, token), ct);
    }

    public Task<ScientificLifecycleCounts> ApplyRetentionAsync(Guid ownerId, Guid operationId, DateTimeOffset cutoff,
        bool confirmed, CancellationToken ct = default)
    {
        var utc = ValidateCutoff(cutoff);
        ValidateOperation(operationId, confirmed);
        return MutationAsync(ownerId, operationId, "retention", utc,
            token => new ScientificRetention(db).ApplyAsync(ownerId, utc, token), ct);
    }

    public Task<ScientificLifecycleCounts> EraseAllAsync(Guid ownerId, Guid operationId, bool confirmed, CancellationToken ct = default)
    {
        ValidateOperation(operationId, confirmed);
        return MutationAsync(ownerId, operationId, "erase", null, token => EraseCoreAsync(ownerId, token), ct);
    }

    public Task<ScientificLifecycleCounts> ResumeIngestionAsync(Guid ownerId, Guid operationId, bool confirmed, CancellationToken ct = default)
    {
        ValidateOperation(operationId, confirmed);
        return MutationAsync(ownerId, operationId, "resume", null, async token =>
        {
            var changes = await db.Set<ScientificLifecycleFenceRow>().Where(x => x.OwnerId == ownerId).ExecuteDeleteAsync(token);
            return new ScientificLifecycleCounts(FenceChanges: changes);
        }, ct);
    }

    public async Task<ScientificLifecycleCounts> ExportArchiveAsync(Guid ownerId, Guid operationId, Stream destination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ValidateOperation(operationId, confirmed: true);
        if (!destination.CanSeek || !destination.CanWrite || destination.Length != 0)
        {
            throw new ArgumentException("L'archivio richiede uno stream vuoto, scrivibile e seekable.", nameof(destination));
        }

        try
        {
            return await LockedAsync(ownerId, async token =>
            {
                destination.SetLength(0);
                destination.Position = 0;
                if (await FindAuditAsync(ownerId, operationId, token) is not null)
                {
                    throw new InvalidOperationException("ID export già utilizzato.");
                }

                var counts = await new ScientificArchiveWriter(db).WriteAsync(ownerId, clock.GetUtcNow(), destination, token);
                await destination.FlushAsync(token);
                await AddAuditAsync(ownerId, operationId, "export", null, counts, token);
                return counts;
            }, ct);
        }
        catch
        {
            destination.SetLength(0);
            destination.Position = 0;
            throw;
        }
    }

    public async Task<IReadOnlyList<ScientificLifecycleAudit>> ReadAuditAsync(Guid ownerId, int limit = 100,
        CancellationToken ct = default)
    {
        if (ownerId == Guid.Empty || limit is < 1 or > 500)
        {
            throw new ArgumentException("Atleta o limite non validi.", nameof(ownerId));
        }

        var rows = await db.Set<ScientificLifecycleAuditRow>().AsNoTracking().Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.At).ThenByDescending(x => x.OperationId).Take(limit).ToListAsync(ct);
        return Array.AsReadOnly(rows.Select(x => new ScientificLifecycleAudit(x.OperationId, x.Action, x.At,
            x.Cutoff, ReadCounts(x))).ToArray());
    }

    private Task<ScientificLifecycleCounts> MutationAsync(Guid ownerId, Guid operationId, string action,
        DateTimeOffset? cutoff, Func<CancellationToken, Task<ScientificLifecycleCounts>> mutate, CancellationToken ct) =>
        LockedAsync(ownerId, async token =>
        {
            var previous = await FindAuditAsync(ownerId, operationId, token);
            if (previous is not null)
            {
                if (!string.Equals(previous.Action, action, StringComparison.Ordinal) || previous.Cutoff != cutoff)
                {
                    throw new InvalidOperationException("ID operazione già utilizzato.");
                }

                return ReadCounts(previous);
            }

            var counts = await mutate(token);
            await AddAuditAsync(ownerId, operationId, action, cutoff, counts, token);
            return counts;
        }, ct);

    private async Task<ScientificLifecycleCounts> EraseCoreAsync(Guid ownerId, CancellationToken ct)
    {
        var fence = db.Set<ScientificLifecycleFenceRow>();
        var changed = 0;
        if (!await fence.AnyAsync(x => x.OwnerId == ownerId, ct))
        {
            fence.Add(new ScientificLifecycleFenceRow { OwnerId = ownerId, BlockedAt = ScientificModelJson.Normalize(clock.GetUtcNow()) });
            changed = 1;
        }

        var runs = await db.Set<ScientificModelRunRow>().Where(x => x.OwnerId == ownerId).ExecuteDeleteAsync(ct);
        var models = await db.Set<ScientificModelArtifactRow>().Where(x => x.OwnerId == ownerId).ExecuteDeleteAsync(ct);
        var feedback = await db.Set<AthleteFeedbackEntryRow>().Where(x => x.OwnerId == ownerId).ExecuteDeleteAsync(ct);
        var signals = await db.Set<ScientificSignalChunkRow>().Where(x => x.OwnerId == ownerId).ExecuteDeleteAsync(ct);
        var checkpoints = await db.Set<ScientificSyncCheckpointRow>().Where(x => x.OwnerId == ownerId).ExecuteDeleteAsync(ct);
        var observations = await db.Set<ScientificObservationRow>().Where(x => x.OwnerId == ownerId).ExecuteDeleteAsync(ct);
        return new ScientificLifecycleCounts(observations, feedback, signals, runs, models, checkpoints, FenceChanges: changed);
    }

    private async Task<T> LockedAsync<T>(Guid ownerId, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Atleta obbligatorio.", nameof(ownerId));
        }

        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Usare uno scope dedicato senza transazioni o modifiche pendenti.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(token);
                await ScientificWriteGate.LockAsync(db, ownerId, token);
                var result = await action(token);
                await tx.CommitAsync(token);
                return result;
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }, ct);
    }

    private Task<ScientificLifecycleAuditRow?> FindAuditAsync(Guid ownerId, Guid operationId, CancellationToken ct) =>
        db.Set<ScientificLifecycleAuditRow>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.OwnerId == ownerId && x.OperationId == operationId, ct);

    private async Task AddAuditAsync(Guid ownerId, Guid operationId, string action, DateTimeOffset? cutoff,
        ScientificLifecycleCounts counts, CancellationToken ct)
    {
        db.Set<ScientificLifecycleAuditRow>().Add(new ScientificLifecycleAuditRow
        {
            OwnerId = ownerId,
            OperationId = operationId,
            Action = action,
            At = ScientificModelJson.Normalize(clock.GetUtcNow()),
            Cutoff = cutoff,
            CountsJson = JsonSerializer.Serialize(counts),
        });
        await db.SaveChangesAsync(ct);
    }

    private static ScientificLifecycleCounts ReadCounts(ScientificLifecycleAuditRow row) =>
        JsonSerializer.Deserialize<ScientificLifecycleCounts>(row.CountsJson) ?? throw new InvalidDataException("Audit non valido.");

    private DateTimeOffset ValidateCutoff(DateTimeOffset cutoff)
    {
        var utc = ScientificModelJson.Normalize(cutoff);
        if (cutoff == default || utc > clock.GetUtcNow())
        {
            throw new ArgumentException("Cutoff non valido.", nameof(cutoff));
        }

        return utc;
    }

    private static void ValidateOperation(Guid operationId, bool confirmed)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Identità operazione obbligatoria.", nameof(operationId));
        }

        if (!confirmed)
        {
            throw new ArgumentException("Conferma esplicita obbligatoria.", nameof(confirmed));
        }
    }
}
