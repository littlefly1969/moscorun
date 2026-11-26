using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Retention con protezione transitiva degli ingressi dei dati conservati.</summary>
internal sealed class ScientificRetention(MoscoRunDbContext db)
{
    private const string KeepSql = """
        WITH RECURSIVE roots(id) AS (
            SELECT id FROM moscorun.scientific_observations WHERE owner_id = {0} AND available_at >= {1}
            UNION SELECT unnest(input_ids) FROM moscorun.scientific_model_runs WHERE owner_id = {0} AND available_at >= {1}
            UNION SELECT unnest(output_ids) FROM moscorun.scientific_model_runs WHERE owner_id = {0} AND available_at >= {1}
            UNION SELECT unnest(observation_ids) FROM moscorun.scientific_feedback_entries WHERE owner_id = {0} AND available_at >= {1}
        ), keep(id) AS (
            SELECT id FROM roots
            UNION
            SELECT unnest(o.input_ids) FROM moscorun.scientific_observations o
            JOIN keep k ON k.id = o.id WHERE o.owner_id = {0}
        )
        """;

    private const string WhereSql = """
         WHERE o.owner_id = {0} AND o.available_at < {1}
         AND NOT EXISTS (SELECT 1 FROM keep k WHERE k.id = o.id)
        """;

    public async Task<ScientificLifecycleCounts> PreviewAsync(Guid ownerId, DateTimeOffset cutoff, CancellationToken ct)
    {
        var old = await db.Set<ScientificObservationRow>().CountAsync(x => x.OwnerId == ownerId && x.AvailableAt < cutoff, ct);
        var observations = await db.Database.SqlQuery<int>(RetentionSql(ownerId, cutoff, delete: false)).SingleAsync(ct);
        var feedback = await db.Set<AthleteFeedbackEntryRow>().CountAsync(x => x.OwnerId == ownerId && x.AvailableAt < cutoff, ct);
        var signals = await db.Set<ScientificSignalChunkRow>().CountAsync(x => x.OwnerId == ownerId && x.AvailableTicks < cutoff.UtcTicks, ct);
        var runs = await db.Set<ScientificModelRunRow>().CountAsync(x => x.OwnerId == ownerId && x.AvailableAt < cutoff, ct);
        var models = await db.Set<ScientificModelArtifactRow>().CountAsync(x => x.OwnerId == ownerId && x.RegisteredAt < cutoff &&
            !db.Set<ScientificModelRunRow>().Any(run => run.OwnerId == ownerId && run.ModelId == x.ModelId &&
                run.Version == x.Version && run.AvailableAt >= cutoff), ct);
        return new ScientificLifecycleCounts(observations, feedback, signals, runs, models, ProtectedObservations: old - observations);
    }

    public async Task<ScientificLifecycleCounts> ApplyAsync(Guid ownerId, DateTimeOffset cutoff, CancellationToken ct)
    {
        var preview = await PreviewAsync(ownerId, cutoff, ct);
        var observations = await db.Database.ExecuteSqlInterpolatedAsync(RetentionSql(ownerId, cutoff, delete: true), ct);
        var feedback = await db.Set<AthleteFeedbackEntryRow>().Where(x => x.OwnerId == ownerId && x.AvailableAt < cutoff).ExecuteDeleteAsync(ct);
        var signals = await db.Set<ScientificSignalChunkRow>().Where(x => x.OwnerId == ownerId && x.AvailableTicks < cutoff.UtcTicks).ExecuteDeleteAsync(ct);
        var runs = await db.Set<ScientificModelRunRow>().Where(x => x.OwnerId == ownerId && x.AvailableAt < cutoff).ExecuteDeleteAsync(ct);
        var models = await db.Set<ScientificModelArtifactRow>().Where(x => x.OwnerId == ownerId && x.RegisteredAt < cutoff &&
            !db.Set<ScientificModelRunRow>().Any(run => run.OwnerId == ownerId && run.ModelId == x.ModelId &&
                run.Version == x.Version)).ExecuteDeleteAsync(ct);
        return new ScientificLifecycleCounts(observations, feedback, signals, runs, models,
            ProtectedObservations: preview.ProtectedObservations);
    }

    private static FormattableString RetentionSql(Guid ownerId, DateTimeOffset cutoff, bool delete) =>
        FormattableStringFactory.Create(KeepSql + (delete
            ? "\nDELETE FROM moscorun.scientific_observations o"
            : "\nSELECT count(*)::integer AS \"Value\" FROM moscorun.scientific_observations o") + WhereSql, ownerId, cutoff);
}
