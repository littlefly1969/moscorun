using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Retention con protezione transitiva di ingressi e metadati dei modelli conservati.</summary>
internal sealed class ScientificRetention(MoscoRunDbContext db)
{
    private const string KeepSql = """
        WITH RECURSIVE roots(id) AS (
            SELECT id FROM moscorun.scientific_observations WHERE owner_id = {0} AND available_at >= {1}
            UNION SELECT unnest(input_ids) FROM moscorun.scientific_model_runs WHERE owner_id = {0} AND available_at >= {1}
            UNION SELECT unnest(output_ids) FROM moscorun.scientific_model_runs WHERE owner_id = {0} AND available_at >= {1}
            UNION SELECT unnest(observation_ids) FROM moscorun.scientific_feedback_entries WHERE owner_id = {0} AND available_at >= {1}
        ), edges(parent, child) AS (
            SELECT id, unnest(input_ids) FROM moscorun.scientific_observations WHERE owner_id = {0}
            UNION ALL
            SELECT out_ids.id, dependencies.id FROM moscorun.scientific_model_runs r
            CROSS JOIN LATERAL unnest(r.output_ids) AS out_ids(id)
            CROSS JOIN LATERAL unnest(r.input_ids || r.output_ids) AS dependencies(id)
            WHERE r.owner_id = {0}
        ), keep(id) AS (
            SELECT id FROM roots
            UNION
            SELECT e.child FROM edges e JOIN keep k ON k.id = e.parent
        )
        """;

    private const string ObservationCandidates = """
        FROM moscorun.scientific_observations o
        WHERE o.owner_id = {0} AND o.available_at < {1}
        AND NOT EXISTS (SELECT 1 FROM keep k WHERE k.id = o.id)
        """;

    private const string RunCandidates = """
        FROM moscorun.scientific_model_runs r
        WHERE r.owner_id = {0} AND r.available_at < {1}
        AND NOT EXISTS (SELECT 1 FROM unnest(r.output_ids) AS out_ids(id) JOIN keep k ON k.id = out_ids.id)
        """;

    private const string ModelCandidates = """
        FROM moscorun.scientific_model_artifacts a
        WHERE a.owner_id = {0} AND a.registered_at < {1}
        AND NOT EXISTS (
            SELECT 1 FROM moscorun.scientific_model_runs r
            WHERE r.owner_id = a.owner_id AND r.model_id = a.model_id AND r.version = a.version
            AND (r.available_at >= {1} OR EXISTS (
                SELECT 1 FROM unnest(r.output_ids) AS out_ids(id) JOIN keep k ON k.id = out_ids.id
            ))
        )
        """;

    public async Task<ScientificLifecycleCounts> PreviewAsync(Guid ownerId, DateTimeOffset cutoff, CancellationToken ct)
    {
        var old = await db.Set<ScientificObservationRow>().CountAsync(x => x.OwnerId == ownerId && x.AvailableAt < cutoff, ct);
        var observations = await CountAsync(ownerId, cutoff, ObservationCandidates, ct);
        var feedback = await db.Set<AthleteFeedbackEntryRow>().CountAsync(x => x.OwnerId == ownerId && x.AvailableAt < cutoff, ct);
        var signals = await db.Set<ScientificSignalChunkRow>().CountAsync(x => x.OwnerId == ownerId && x.AvailableTicks < cutoff.UtcTicks, ct);
        var runs = await CountAsync(ownerId, cutoff, RunCandidates, ct);
        var models = await CountAsync(ownerId, cutoff, ModelCandidates, ct);
        var diagnostics = await db.Set<ScienceDiagnosticRow>().CountAsync(x => x.OwnerId == ownerId && x.ReceivedAt < cutoff, ct);
        return new ScientificLifecycleCounts(observations, feedback, signals, runs, models,
            ProtectedObservations: old - observations, Diagnostics: diagnostics);
    }

    public async Task<ScientificLifecycleCounts> ApplyAsync(Guid ownerId, DateTimeOffset cutoff, CancellationToken ct)
    {
        var preview = await PreviewAsync(ownerId, cutoff, ct);
        var observations = await DeleteAsync(ownerId, cutoff, ObservationCandidates, ct);
        var feedback = await db.Set<AthleteFeedbackEntryRow>().Where(x => x.OwnerId == ownerId && x.AvailableAt < cutoff).ExecuteDeleteAsync(ct);
        var signals = await db.Set<ScientificSignalChunkRow>().Where(x => x.OwnerId == ownerId && x.AvailableTicks < cutoff.UtcTicks).ExecuteDeleteAsync(ct);
        var runs = await DeleteAsync(ownerId, cutoff, RunCandidates, ct);
        var models = await db.Set<ScientificModelArtifactRow>().Where(x => x.OwnerId == ownerId && x.RegisteredAt < cutoff &&
            !db.Set<ScientificModelRunRow>().Any(run => run.OwnerId == ownerId && run.ModelId == x.ModelId &&
                run.Version == x.Version)).ExecuteDeleteAsync(ct);
        var diagnostics = await db.Set<ScienceDiagnosticRow>().Where(x => x.OwnerId == ownerId && x.ReceivedAt < cutoff).ExecuteDeleteAsync(ct);
        return new ScientificLifecycleCounts(observations, feedback, signals, runs, models,
            ProtectedObservations: preview.ProtectedObservations, Diagnostics: diagnostics);
    }

    private Task<int> CountAsync(Guid ownerId, DateTimeOffset cutoff, string candidates, CancellationToken ct) =>
        db.Database.SqlQuery<int>(RetentionSql(ownerId, cutoff, "SELECT count(*)::integer AS \"Value\" ", candidates)).SingleAsync(ct);

    private Task<int> DeleteAsync(Guid ownerId, DateTimeOffset cutoff, string candidates, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(RetentionSql(ownerId, cutoff, "DELETE ", candidates), ct);

    private static FormattableString RetentionSql(Guid ownerId, DateTimeOffset cutoff, string operation, string candidates) =>
        FormattableStringFactory.Create(KeepSql + "\n" + operation + candidates, ownerId, cutoff);
}
