using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Lock transazionale PostgreSQL per atleta; tutti gli scrittori scientifici passano da qui.</summary>
internal static class ScientificWriteGate
{
    public static async Task LockAsync(MoscoRunDbContext db, Guid ownerId, CancellationToken ct)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Atleta obbligatorio.", nameof(ownerId));
        }

        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Il lock richiede una transazione.");
        }

        var key = BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(ownerId.ToByteArray()));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }

    public static async Task AssertWritableAsync(MoscoRunDbContext db, Guid ownerId, CancellationToken ct)
    {
        await LockAsync(db, ownerId, ct);
        if (await db.Set<ScientificLifecycleFenceRow>().AnyAsync(x => x.OwnerId == ownerId, ct))
        {
            throw new InvalidOperationException("Acquisizione scientifica sospesa per cancellazione dati.");
        }
    }

    public static async Task<T> RunAsync<T>(MoscoRunDbContext db, Guid ownerId,
        Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            await AssertWritableAsync(db, ownerId, ct);
            return await action(ct);
        }

        if (db.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Usare uno scope dedicato.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(token);
                await AssertWritableAsync(db, ownerId, token);
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
}
