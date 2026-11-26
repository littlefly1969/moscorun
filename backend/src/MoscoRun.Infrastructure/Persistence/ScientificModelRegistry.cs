using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Artefatti e metadati immutabili e privati dell'atleta.</summary>
public sealed class ScientificModelRegistry(MoscoRunDbContext db, TimeProvider clock)
{
    public const int MaxArtifactBytes = 16 * 1024 * 1024;

    public Task<bool> RegisterAsync(Guid ownerId, ScientificModelArtifact model, CancellationToken ct = default) =>
        ScientificWriteGate.RunAsync(db, ownerId, token => RegisterCoreAsync(ownerId, model, token), ct);

    private async Task<bool> RegisterCoreAsync(Guid ownerId, ScientificModelArtifact model, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        var now = ScientificModelJson.Normalize(clock.GetUtcNow());
        Validate(ownerId, model, now);
        if (db.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Usare uno scope dedicato.");
        }

        var row = new ScientificModelArtifactRow
        {
            OwnerId = ownerId,
            ModelId = model.ModelId,
            Version = model.Version,
            Format = model.Format,
            Artifact = model.Artifact.ToArray(),
            ArtifactHash = SHA256.HashData(model.Artifact),
            FeatureSchemaVersion = model.FeatureSchemaVersion,
            TrainingCutoff = ScientificModelJson.Normalize(model.TrainingCutoff),
            RegisteredAt = now,
            ValidationJson = model.ValidationJson,
            ConfigurationJson = model.ConfigurationJson,
        };
        var set = db.Set<ScientificModelArtifactRow>();
        var existing = await FindAsync(ownerId, model.ModelId, model.Version, ct);
        if (existing is not null)
        {
            VerifyReplay(existing, row);
            return false;
        }

        set.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(row).State = EntityState.Detached;
            existing = await FindAsync(ownerId, model.ModelId, model.Version, ct);
            if (existing is null)
            {
                throw;
            }

            VerifyReplay(existing, row);
            return false;
        }
    }

    public async Task<ScientificModelArtifactSnapshot?> ReadAsync(Guid ownerId, string modelId, string version,
        DateTimeOffset asOf, CancellationToken ct = default)
    {
        ValidateKey(ownerId, modelId, version);
        var row = await FindAsync(ownerId, modelId, version, ct);
        if (row is null || row.RegisteredAt > asOf)
        {
            return null;
        }

        if (!SHA256.HashData(row.Artifact).AsSpan().SequenceEqual(row.ArtifactHash))
        {
            throw new InvalidDataException("Artefatto non integro.");
        }

        return new ScientificModelArtifactSnapshot(new ScientificModelArtifact(row.ModelId, row.Version,
            row.Format, row.Artifact, row.FeatureSchemaVersion, row.TrainingCutoff, row.ValidationJson, row.ConfigurationJson),
            Convert.ToHexString(row.ArtifactHash), row.RegisteredAt);
    }

    private Task<ScientificModelArtifactRow?> FindAsync(Guid ownerId, string modelId, string version, CancellationToken ct) =>
        db.Set<ScientificModelArtifactRow>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.OwnerId == ownerId && x.ModelId == modelId && x.Version == version, ct);

    internal static void ValidateKey(Guid ownerId, string modelId, string version)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Atleta obbligatorio.", nameof(ownerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (modelId.Length > 200 || version.Length > 200)
        {
            throw new ArgumentException("Identità modello troppo lunga.", nameof(modelId));
        }
    }

    private static void Validate(Guid ownerId, ScientificModelArtifact model, DateTimeOffset now)
    {
        ValidateKey(ownerId, model.ModelId, model.Version);
        if (string.IsNullOrWhiteSpace(model.Format) || model.Format.Length > 64 ||
            string.IsNullOrWhiteSpace(model.FeatureSchemaVersion) || model.FeatureSchemaVersion.Length > 200 ||
            model.Artifact is null || model.Artifact.Length is < 1 or > MaxArtifactBytes ||
            model.TrainingCutoff == default || ScientificModelJson.Normalize(model.TrainingCutoff) > now)
        {
            throw new ArgumentException("Artefatto o metadati non validi.", nameof(model));
        }

        ScientificModelJson.ValidateObject(model.ValidationJson);
        ScientificModelJson.ValidateObject(model.ConfigurationJson);
    }

    private static void VerifyReplay(ScientificModelArtifactRow existing, ScientificModelArtifactRow row)
    {
        if (!existing.ArtifactHash.AsSpan().SequenceEqual(row.ArtifactHash) ||
            !string.Equals(existing.Format, row.Format, StringComparison.Ordinal) ||
            !string.Equals(existing.FeatureSchemaVersion, row.FeatureSchemaVersion, StringComparison.Ordinal) ||
            existing.TrainingCutoff != row.TrainingCutoff ||
            !ScientificModelJson.Equal(existing.ValidationJson, row.ValidationJson) ||
            !ScientificModelJson.Equal(existing.ConfigurationJson, row.ConfigurationJson))
        {
            throw new InvalidOperationException("Versione modello già presente con contenuto diverso.");
        }
    }
}
