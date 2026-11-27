using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Archivio JSON Lines versionato, lettura a blocchi su transazione coerente.</summary>
internal sealed class ScientificArchiveWriter(MoscoRunDbContext db)
{
    public async Task<ScientificLifecycleCounts> WriteAsync(Guid ownerId, DateTimeOffset at, Stream destination, CancellationToken ct)
    {
        await WriteLineAsync(destination, "manifest", new { schemaVersion = 1, ownerId, exportedAt = at }, ct);
        var observations = await WriteRowsAsync(destination, "observation", db.Set<ScientificObservationRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId).OrderBy(x => x.Id), 100, ct);
        var feedback = await WriteRowsAsync(destination, "feedback", db.Set<AthleteFeedbackEntryRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId).OrderBy(x => x.Id), 100, ct);
        var signals = await WriteRowsAsync(destination, "signal", db.Set<ScientificSignalChunkRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId).OrderBy(x => x.Id), 4, ct);
        var runs = await WriteRowsAsync(destination, "modelRun", db.Set<ScientificModelRunRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId).OrderBy(x => x.Id), 20, ct);
        var models = await WriteRowsAsync(destination, "modelArtifact", db.Set<ScientificModelArtifactRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId).OrderBy(x => x.ModelId).ThenBy(x => x.Version), 1, ct);
        var checkpoints = await WriteRowsAsync(destination, "syncCheckpoint", db.Set<ScientificSyncCheckpointRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId).OrderBy(x => x.Source).ThenBy(x => x.Stream)
            .Select(x => new { x.Source, x.Stream, x.UpdatedAt }), 100, ct);
        _ = await WriteRowsAsync(destination, "audit", db.Set<ScientificLifecycleAuditRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId).OrderBy(x => x.OperationId), 100, ct);
        _ = await WriteRowsAsync(destination, "ingestionFence", db.Set<ScientificLifecycleFenceRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId).OrderBy(x => x.OwnerId), 1, ct);
        var diagnostics = await WriteRowsAsync(destination, "scienceDiagnostic", db.Set<ScienceDiagnosticRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId).OrderBy(x => x.DeviceId).ThenBy(x => x.Source), 100, ct);
        return new ScientificLifecycleCounts(observations, feedback, signals, runs, models, checkpoints, Diagnostics: diagnostics);
    }

    private static async Task<int> WriteRowsAsync<T>(Stream destination, string kind, IQueryable<T> query, int batch,
        CancellationToken ct)
    {
        var count = 0;
        while (true)
        {
            var rows = await query.Skip(count).Take(batch).ToListAsync(ct);
            foreach (var row in rows)
            {
                await WriteLineAsync(destination, kind, row, ct);
            }

            count = checked(count + rows.Count);
            if (rows.Count < batch)
            {
                return count;
            }
        }
    }

    private static async Task WriteLineAsync<T>(Stream destination, string kind, T payload, CancellationToken ct)
    {
        await JsonSerializer.SerializeAsync(destination, new { kind, payload }, cancellationToken: ct);
        await destination.WriteAsync("\n"u8.ToArray(), ct);
    }
}
