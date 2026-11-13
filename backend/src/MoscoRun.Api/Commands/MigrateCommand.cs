using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Infrastructure.Identity;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Commands;

/// <summary>
/// Passo esplicito di rilascio: applica le migrazioni e, al primo avvio, crea l'utente amministratore
/// da <c>Bootstrap:AdminEmail</c>/<c>Bootstrap:AdminPassword</c>.
/// </summary>
/// <remarks>
/// Codici di uscita: 0 riuscito, 2 amministratore non creato, 3 migrazione "contract" in attesa senza
/// <c>--allow-contract</c> (nessuna modifica applicata). Con <c>--plan</c> elenca le migrazioni senza applicarle.
/// </remarks>
internal static partial class MigrateCommand
{
    public const int ContractMigrationRefused = 3;

    public static async Task<int> RunAsync(IServiceProvider services, IReadOnlyCollection<string>? args = null)
    {
        args ??= [];
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("MoscoRun.Migrate");
        var db = provider.GetRequiredService<MoscoRunDbContext>();

        // Preflight: nessuna modifica allo schema se la versione in esecuzione potrebbe non sopportarla.
        var pending = await MigrationSafety.GetPendingAsync(db);
        List<string> ids = [.. pending.Select(p => p.Id)];
        LogPending(logger, ids.Count, ids);
        var contract = pending.Where(p => p.IsContract).ToList();
        foreach (var migration in contract)
        {
            LogContract(logger, migration.Id, string.Join(", ", migration.BreakingOperations));
        }

        if (args.Contains("--plan", StringComparer.Ordinal))
        {
            return contract.Count > 0 ? ContractMigrationRefused : 0;
        }

        if (contract.Count > 0 && !args.Contains("--allow-contract", StringComparer.Ordinal))
        {
            LogContractRefused(logger);
            return ContractMigrationRefused;
        }

        await db.Database.MigrateAsync();

        var config = provider.GetRequiredService<IConfiguration>();
        var email = config["Bootstrap:AdminEmail"];
        var password = config["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            LogDone(logger);
            return 0;
        }

        var users = provider.GetRequiredService<UserManager<AppUser>>();
        if (await users.Users.AnyAsync())
        {
            LogAdminSkipped(logger);
            return 0;
        }

        var result = await users.CreateAsync(new AppUser { UserName = email, Email = email, EmailConfirmed = true }, password);
        if (!result.Succeeded)
        {
            LogAdminFailed(logger, string.Join("; ", result.Errors.Select(e => e.Description)));
            return 2;
        }

        LogAdminCreated(logger, email);
        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrazioni da applicare: {Count} {Names}")]
    private static partial void LogPending(ILogger logger, int count, IReadOnlyList<string> names);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Migrazione contract (incompatibile con la versione precedente): {Id} {Operations}")]
    private static partial void LogContract(ILogger logger, string id, string operations);

    [LoggerMessage(Level = LogLevel.Error, Message = "Migrazioni contract in attesa: nulla è stato applicato. Fare un backup e rilanciare con --allow-contract (vedi docs/deploy-linux.md)")]
    private static partial void LogContractRefused(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database aggiornato")]
    private static partial void LogDone(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Utenti già presenti: creazione amministratore saltata")]
    private static partial void LogAdminSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Creazione amministratore fallita: {Errors}")]
    private static partial void LogAdminFailed(ILogger logger, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Amministratore creato: {Email}")]
    private static partial void LogAdminCreated(ILogger logger, string email);
}
