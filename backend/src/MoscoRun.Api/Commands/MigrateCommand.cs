using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Infrastructure.Identity;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Commands;

/// <summary>
/// Passo esplicito di rilascio: applica le migrazioni e, al primo avvio, crea l'utente amministratore
/// da <c>Bootstrap:AdminEmail</c>/<c>Bootstrap:AdminPassword</c>.
/// </summary>
internal static partial class MigrateCommand
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("MoscoRun.Migrate");
        var db = provider.GetRequiredService<MoscoRunDbContext>();

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        LogPending(logger, pending.Count, pending);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Database aggiornato")]
    private static partial void LogDone(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Utenti già presenti: creazione amministratore saltata")]
    private static partial void LogAdminSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Creazione amministratore fallita: {Errors}")]
    private static partial void LogAdminFailed(ILogger logger, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Amministratore creato: {Email}")]
    private static partial void LogAdminCreated(ILogger logger, string email);
}
