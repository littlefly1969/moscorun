using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Hosting;

/// <summary>
/// Readiness funzionale: oltre alla connessione verifica che lo schema corrisponda al modello di questa versione.
/// Legge al più una riga da ogni tabella usata dall'applicazione selezionando tutte le colonne mappate, quindi una
/// colonna mancante, rinominata o di tipo incompatibile rende l'istanza "non pronta" prima di servire traffico.
/// Fallisce anche se restano migrazioni non applicate (immagine nuova su schema vecchio).
/// </summary>
internal sealed class SchemaHealthCheck(MoscoRunDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            return HealthCheckResult.Unhealthy($"Migrazioni non applicate: {string.Join(", ", pending)}");
        }

        try
        {
            _ = await db.Weighings.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            _ = await db.Devices.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            _ = await db.Users.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            _ = await db.DataProtectionKeys.AsNoTracking().Take(1).ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is Npgsql.PostgresException or InvalidOperationException or InvalidCastException)
        {
            return HealthCheckResult.Unhealthy("Lo schema del database non corrisponde al modello dell'applicazione.", ex);
        }

        return HealthCheckResult.Healthy();
    }
}
