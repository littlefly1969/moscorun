using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Registro delle esecuzioni e stato Digital Twin con lineage point-in-time.</summary>
public sealed class ScientificModelRunStore(MoscoRunDbContext db, ScientificObservationStore observations, TimeProvider clock)
{
    public async Task<bool> SaveAsync(Guid ownerId, ScientificModelRun run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        var now = ScientificModelJson.Normalize(clock.GetUtcNow());
        Validate(ownerId, run, now);
        var operational = run.Mode == ScientificModelRunMode.Operational;
        var decision = ScientificModelJson.Normalize(run.DecisionAt);
        var outputs = run.Outputs.OrderBy(x => x.Id).ToArray();
        var inputs = run.InputIds.Order().ToArray();
        var outputObservations = outputs.Select(x => new ScientificObservation(x.Id, ownerId, x.Metric, x.Unit,
            x.Value, decision, now, run.ModelId, ObservationMethod.ModelInferred, derivationVersion: run.Version, inputIds: inputs)).ToArray();
        if (db.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Usare uno scope dedicato.");
        }

        var row = new ScientificModelRunRow
        {
            OwnerId = ownerId,
            Id = run.Id,
            ModelId = run.ModelId,
            Version = run.Version,
            Mode = run.Mode.ToString(),
            DecisionAt = decision,
            TargetAt = run.TargetAt is { } target ? ScientificModelJson.Normalize(target) : null,
            AvailableAt = now,
            InputIds = inputs,
            OutputIds = operational ? outputs.Select(x => x.Id).ToArray() : [],
            OutputsJson = JsonSerializer.Serialize(outputs),
            StateJson = run.StateJson,
        };
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(token);
                var result = await PersistAsync(row, operational ? outputObservations : [], token);
                await tx.CommitAsync(token);
                return result;
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }, ct);
    }

    private async Task<bool> PersistAsync(ScientificModelRunRow row, ScientificObservation[] outputs, CancellationToken ct)
    {
        var set = db.Set<ScientificModelRunRow>();
        var old = await set.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == row.OwnerId && x.Id == row.Id, ct);
        if (old is not null)
        {
            VerifyReplay(old, row);
            return false;
        }

        await ValidateModelAsync(row, ct);
        await ValidateInputsAsync(row, ct);
        foreach (var output in outputs)
        {
            await observations.AppendAsync(row.OwnerId, output, ct);
        }

        set.Add(row);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ScientificModelRunSnapshot?> ReadAsync(Guid ownerId, Guid runId, DateTimeOffset asOf,
        CancellationToken ct = default)
    {
        if (ownerId == Guid.Empty || runId == Guid.Empty)
        {
            throw new ArgumentException("Identità non valida.", nameof(runId));
        }

        var cutoff = asOf.ToUniversalTime();
        var row = await db.Set<ScientificModelRunRow>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.OwnerId == ownerId && x.Id == runId && x.AvailableAt <= cutoff, ct);
        return row is null ? null : ToSnapshot(row);
    }

    public async Task<ScientificModelRunSnapshot?> ReadLatestStateAsync(Guid ownerId, string modelId, string version,
        DateTimeOffset asOf, CancellationToken ct = default)
    {
        ScientificModelRegistry.ValidateKey(ownerId, modelId, version);
        var cutoff = asOf.ToUniversalTime();
        var operational = ScientificModelRunMode.Operational.ToString();
        var row = await db.Set<ScientificModelRunRow>().AsNoTracking().Where(x => x.OwnerId == ownerId &&
            x.ModelId == modelId && x.Version == version && x.Mode == operational &&
            x.StateJson != null && x.AvailableAt <= cutoff)
            .OrderByDescending(x => x.DecisionAt).ThenByDescending(x => x.AvailableAt).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        return row is null ? null : ToSnapshot(row);
    }

    private async Task ValidateModelAsync(ScientificModelRunRow row, CancellationToken ct)
    {
        var model = await db.Set<ScientificModelArtifactRow>().AsNoTracking().Where(x => x.OwnerId == row.OwnerId &&
            x.ModelId == row.ModelId && x.Version == row.Version)
            .Select(x => new { x.TrainingCutoff, x.RegisteredAt }).SingleOrDefaultAsync(ct);
        if (model is null || model.TrainingCutoff > row.DecisionAt || model.RegisteredAt > row.AvailableAt ||
            (string.Equals(row.Mode, nameof(ScientificModelRunMode.Operational), StringComparison.Ordinal) && model.RegisteredAt > row.DecisionAt))
        {
            throw new InvalidOperationException("Versione modello non disponibile o training successivo alla decisione.");
        }
    }

    private async Task ValidateInputsAsync(ScientificModelRunRow row, CancellationToken ct)
    {
        const int maxLineage = 10_000;
        var pending = new HashSet<Guid>(row.InputIds);
        var seen = new Dictionary<Guid, ScientificObservation>();
        while (pending.Count > 0)
        {
            var ids = pending.Take(500).ToArray();
            pending.ExceptWith(ids);
            var values = await db.Set<ScientificObservationRow>().AsNoTracking().Where(x => x.OwnerId == row.OwnerId &&
                ids.Contains(x.Id) && x.AvailableAt <= row.DecisionAt).ToListAsync(ct);
            if (values.Count != ids.Length)
            {
                throw new InvalidOperationException("Ingressi non disponibili alla decisione.");
            }

            foreach (var value in values)
            {
                seen.Add(value.Id, new ScientificObservation(value.Id, value.OwnerId, value.Metric, value.Unit,
                    value.Value, value.Time, value.AvailableAt, value.Source, Enum.Parse<ObservationMethod>(value.Method),
                    value.ProviderAlgorithm, value.DerivationVersion, value.InputIds));
                pending.UnionWith(value.InputIds.Where(id => !seen.ContainsKey(id)));
            }

            pending.ExceptWith(seen.Keys);
            if (seen.Count + pending.Count > maxLineage)
            {
                throw new InvalidOperationException("Lineage oltre il limite di validazione.");
            }
        }

        var valid = ScientificSnapshot.Select(row.OwnerId, row.DecisionAt, seen.Values).Select(x => x.Id).ToHashSet();
        if (!row.InputIds.All(valid.Contains))
        {
            throw new InvalidOperationException("Lineage non valido alla decisione.");
        }
    }

    private static void Validate(Guid ownerId, ScientificModelRun run, DateTimeOffset now)
    {
        ScientificModelRegistry.ValidateKey(ownerId, run.ModelId, run.Version);
        if (run.Id == Guid.Empty || !Enum.IsDefined(run.Mode) || run.DecisionAt == default ||
            ScientificModelJson.Normalize(run.DecisionAt) > now || run.TargetAt < run.DecisionAt ||
            run.InputIds is null || run.InputIds.Count is < 1 or > 500 || run.InputIds.Contains(Guid.Empty) ||
            run.InputIds.Distinct().Count() != run.InputIds.Count || run.Outputs is null ||
            run.Outputs.Count > 128 || (run.Outputs.Count == 0 && run.StateJson is null) ||
            run.Outputs.Select(x => x.Id).Distinct().Count() != run.Outputs.Count ||
            run.Outputs.Any(x => x.Id == Guid.Empty || run.InputIds.Contains(x.Id) || !ValidInterval(x)))
        {
            throw new ArgumentException("Esecuzione modello non valida.", nameof(run));
        }

        if (run.StateJson is not null)
        {
            ScientificModelJson.ValidateObject(run.StateJson);
        }
    }

    private static bool ValidInterval(ScientificModelOutput output)
    {
        if (output.Lower is null && output.Upper is null)
        {
            return true;
        }

        return output.Value is { } value && output.Lower is { } lower && output.Upper is { } upper &&
            double.IsFinite(lower) && double.IsFinite(upper) && lower <= value && value <= upper;
    }

    private static void VerifyReplay(ScientificModelRunRow old, ScientificModelRunRow row)
    {
        if (!string.Equals(old.ModelId, row.ModelId, StringComparison.Ordinal) ||
            !string.Equals(old.Version, row.Version, StringComparison.Ordinal) ||
            !string.Equals(old.Mode, row.Mode, StringComparison.Ordinal) || old.DecisionAt != row.DecisionAt ||
            old.TargetAt != row.TargetAt || !old.InputIds.SequenceEqual(row.InputIds) ||
            !ScientificModelJson.Equal(old.OutputsJson, row.OutputsJson) || !ScientificModelJson.Equal(old.StateJson, row.StateJson))
        {
            throw new InvalidOperationException("Esecuzione già presente con contenuto diverso.");
        }
    }

    private static ScientificModelRunSnapshot ToSnapshot(ScientificModelRunRow row) => new(new ScientificModelRun(
        row.Id, row.ModelId, row.Version, Enum.Parse<ScientificModelRunMode>(row.Mode), row.DecisionAt, row.TargetAt,
        Array.AsReadOnly(row.InputIds), Array.AsReadOnly(JsonSerializer.Deserialize<ScientificModelOutput[]>(row.OutputsJson)
            ?? throw new InvalidDataException("Output modello non validi.")), row.StateJson), row.AvailableAt);
}
