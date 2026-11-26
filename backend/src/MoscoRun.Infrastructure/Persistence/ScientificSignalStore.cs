using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;
using Npgsql;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Chunk lossless versionati, limitati e idempotenti per segnali ad alta frequenza.</summary>
public sealed class ScientificSignalStore(MoscoRunDbContext db, TimeProvider clock)
{
    public const int MaxPageSize = 16;

    public Task<bool> ImportAsync(Guid ownerId, ScientificSignalChunk chunk, CancellationToken ct = default) =>
        ScientificWriteGate.RunAsync(db, ownerId, token => ImportCoreAsync(ownerId, chunk, token), ct);

    private async Task<bool> ImportCoreAsync(Guid ownerId, ScientificSignalChunk chunk, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        var row = BuildRow(ownerId, chunk, clock.GetUtcNow());
        await ValidateContextAsync(ownerId, chunk, ct);
        if (db.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Usare uno scope dedicato.");
        }

        var set = db.Set<ScientificSignalChunkRow>();
        var existing = await set.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == row.Id, ct);
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
            existing = await set.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == row.Id, ct);
            if (existing is null)
            {
                throw;
            }

            VerifyReplay(existing, row);
            return false;
        }
    }

    public async Task<ScientificSignalPage> ReadWindowAsync(Guid ownerId, string source, string metric,
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset asOf, ScientificSignalCursor? after = null,
        int limit = 4, CancellationToken ct = default)
    {
        if (ownerId == Guid.Empty || from >= to || limit is < 1 or > MaxPageSize)
        {
            throw new ArgumentException("Finestra non valida.", nameof(from));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(metric);
        var start = from.UtcTicks;
        var end = to.UtcTicks;
        var cutoff = asOf.UtcTicks;
        var query = db.Set<ScientificSignalChunkRow>().AsNoTracking().Where(x => x.OwnerId == ownerId &&
            x.Source == source && x.Metric == metric && x.FirstTicks < end && x.LastTicks >= start && x.AvailableTicks <= cutoff);
        if (after is not null)
        {
            query = query.Where(x => x.FirstTicks > after.FirstTicks ||
                (x.FirstTicks == after.FirstTicks && x.Id.CompareTo(after.Id) > 0));
        }

        var rows = await query.OrderBy(x => x.FirstTicks).ThenBy(x => x.Id).Take(limit + 1).ToListAsync(ct);
        var items = rows.Take(limit).Select(ToChunk).ToArray();
        var next = rows.Count > limit ? new ScientificSignalCursor(rows[limit - 1].FirstTicks, rows[limit - 1].Id) : null;
        return new ScientificSignalPage(Array.AsReadOnly(items), next);
    }

    private static ScientificSignalChunkRow BuildRow(Guid ownerId, ScientificSignalChunk chunk, DateTimeOffset now)
    {
        if (chunk.Method is not (ObservationMethod.Measured or ObservationMethod.ProviderReported or ObservationMethod.ProviderEstimated))
        {
            throw new ArgumentException("Metodo del segnale non valido.", nameof(chunk));
        }

        var encoded = ScientificSignalCodec.Encode(chunk.Samples);
        var id = ScientificImportStore.Identity(new ScientificImportItem(chunk.Source, chunk.ExternalId, chunk.Revision,
            chunk.Metric, chunk.Unit, null, chunk.Samples[0].Time, chunk.Method, chunk.ProviderAlgorithm, chunk.RevisionOrder));
        _ = new ScientificObservation(id, ownerId, chunk.Metric, chunk.Unit, null, chunk.Samples[^1].Time,
            now, chunk.Source, chunk.Method, chunk.ProviderAlgorithm);
        return new ScientificSignalChunkRow
        {
            OwnerId = ownerId,
            Id = id,
            Source = chunk.Source,
            ExternalId = chunk.ExternalId,
            Revision = chunk.Revision,
            RevisionOrder = chunk.RevisionOrder,
            Metric = chunk.Metric,
            Unit = chunk.Unit,
            Method = chunk.Method.ToString(),
            ProviderAlgorithm = chunk.ProviderAlgorithm,
            DeviceId = chunk.DeviceId,
            WorkoutId = chunk.WorkoutId,
            FirstTicks = chunk.Samples[0].Time.UtcTicks,
            LastTicks = chunk.Samples[^1].Time.UtcTicks,
            AvailableTicks = now.UtcTicks,
            SampleCount = chunk.Samples.Count,
            CodecVersion = ScientificSignalCodec.Version,
            Data = encoded.Data,
            ContentHash = encoded.Hash,
        };
    }

    private async Task ValidateContextAsync(Guid ownerId, ScientificSignalChunk chunk, CancellationToken ct)
    {
        if (chunk.DeviceId is { } deviceId && !await db.Devices.AnyAsync(x => x.OwnerId == ownerId &&
                x.Id == deviceId && x.RevokedAt == null, ct))
        {
            throw new ArgumentException("Dispositivo non autorizzato.", nameof(chunk));
        }

        if (chunk.WorkoutId is { } workoutId && !await db.Workouts.AnyAsync(x => x.OwnerId == ownerId && x.Id == workoutId, ct))
        {
            throw new ArgumentException("Allenamento non autorizzato.", nameof(chunk));
        }
    }

    private static void VerifyReplay(ScientificSignalChunkRow existing, ScientificSignalChunkRow row)
    {
        if (!string.Equals(existing.Source, row.Source, StringComparison.Ordinal) ||
            !string.Equals(existing.ExternalId, row.ExternalId, StringComparison.Ordinal) ||
            !string.Equals(existing.Revision, row.Revision, StringComparison.Ordinal) ||
            !string.Equals(existing.Metric, row.Metric, StringComparison.Ordinal) ||
            !string.Equals(existing.Unit, row.Unit, StringComparison.Ordinal) ||
            !string.Equals(existing.Method, row.Method, StringComparison.Ordinal) ||
            !string.Equals(existing.ProviderAlgorithm, row.ProviderAlgorithm, StringComparison.Ordinal) ||
            existing.RevisionOrder != row.RevisionOrder || existing.WorkoutId != row.WorkoutId ||
            existing.SampleCount != row.SampleCount || !existing.ContentHash.AsSpan().SequenceEqual(row.ContentHash))
        {
            throw new InvalidOperationException("Revisione del segnale già presente con contenuto diverso.");
        }
    }

    private static ScientificStoredSignalChunk ToChunk(ScientificSignalChunkRow row) => new(row.Id,
        new DateTimeOffset(row.AvailableTicks, TimeSpan.Zero),
        new ScientificSignalChunk(row.Source, row.ExternalId, row.Revision, row.Metric, row.Unit,
            Enum.Parse<ObservationMethod>(row.Method), ScientificSignalCodec.Decode(row.Data, row.ContentHash, row.SampleCount, row.CodecVersion),
            row.ProviderAlgorithm, row.RevisionOrder, row.DeviceId, row.WorkoutId));
}
