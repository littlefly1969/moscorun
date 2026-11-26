using Microsoft.EntityFrameworkCore;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Importa una pagina e avanza il cursore in una sola transazione PostgreSQL.</summary>
public sealed class ScientificSyncStore(MoscoRunDbContext db, ScientificImportStore imports, TimeProvider clock)
{
    public async Task<string?> ReadCursorAsync(Guid ownerId, string source, string stream, CancellationToken ct = default)
    {
        ValidateKey(ownerId, source, stream);
        return await db.Set<ScientificSyncCheckpointRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId && x.Source == source && x.Stream == stream)
            .Select(x => x.Cursor).SingleOrDefaultAsync(ct);
    }

    public async Task<int> ImportPageAsync(Guid ownerId, ScientificSyncPage page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ValidateKey(ownerId, page.Source, page.Stream);
        if (string.IsNullOrWhiteSpace(page.NextCursor) || page.NextCursor.Length > 2048 || page.PreviousCursor?.Length > 2048 ||
            page.Items is null || page.Items.Any(x => !string.Equals(x.Source, page.Source, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Pagina di sincronizzazione non valida.", nameof(page));
        }

        if (db.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Usare uno scope dedicato.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async cancellationToken =>
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                var count = await ImportAndAdvanceAsync(ownerId, page, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return count;
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }, ct);
    }

    private async Task<int> ImportAndAdvanceAsync(Guid ownerId, ScientificSyncPage page, CancellationToken ct)
    {
        var checkpoints = db.Set<ScientificSyncCheckpointRow>();
        var checkpoint = await checkpoints.SingleOrDefaultAsync(x => x.OwnerId == ownerId &&
            x.Source == page.Source && x.Stream == page.Stream, ct);
        var current = checkpoint?.Cursor;
        var replay = checkpoint is not null && string.Equals(current, page.NextCursor, StringComparison.Ordinal);
        if (!replay && !string.Equals(current, page.PreviousCursor, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cursore obsoleto: rileggere lo stato di sincronizzazione.");
        }

        var count = await imports.ImportAsync(ownerId, page.Items, ct);
        if (replay)
        {
            if (count != 0)
            {
                throw new InvalidOperationException("Replay del cursore con campioni nuovi.");
            }

            return 0;
        }

        if (checkpoint is null)
        {
            checkpoints.Add(new ScientificSyncCheckpointRow
            {
                OwnerId = ownerId,
                Source = page.Source,
                Stream = page.Stream,
                Cursor = page.NextCursor,
                UpdatedAt = clock.GetUtcNow(),
            });
        }
        else
        {
            checkpoint.Cursor = page.NextCursor;
            checkpoint.UpdatedAt = clock.GetUtcNow();
        }

        await db.SaveChangesAsync(ct);
        return count;
    }

    private static void ValidateKey(Guid ownerId, string source, string stream)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Atleta obbligatorio.", nameof(ownerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        if (source.Length > 200 || stream.Length > 200)
        {
            throw new ArgumentException("Chiave di sincronizzazione troppo lunga.", nameof(stream));
        }
    }
}
