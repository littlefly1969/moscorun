using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>
/// Regola expand/contract: il rollback automatico riporta l'immagine precedente ma non lo schema,
/// quindi ogni migrazione applicata in automatico deve lasciare funzionante anche la versione precedente.
/// </summary>
public static class MigrationSafety
{
    /// <summary>Operazioni che possono rompere la versione precedente dell'applicazione.</summary>
    public static IReadOnlyList<string> FindBreakingOperations(Migration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);
        var operations = migration.UpOperations;
        var createdTables = operations.OfType<CreateTableOperation>()
            .Select(t => (t.Schema, t.Name))
            .ToHashSet();

        var breaking = new List<string>();
        foreach (var operation in operations)
        {
            var description = Describe(operation, createdTables);
            if (description is not null)
            {
                breaking.Add(description);
            }
        }

        return breaking;
    }

    /// <summary>Migrazioni non ancora applicate al database, nell'ordine in cui verrebbero eseguite.</summary>
    public static async Task<IReadOnlyList<PendingMigration>> GetPendingAsync(DbContext db, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var assembly = db.GetService<IMigrationsAssembly>();
        var provider = db.Database.ProviderName ?? throw new InvalidOperationException("Provider del database sconosciuto.");
        var pending = await db.Database.GetPendingMigrationsAsync(ct);

        return [.. pending.Select(id =>
        {
            var type = assembly.Migrations[id];
            var migration = assembly.CreateMigration(type, provider);
            var breaking = FindBreakingOperations(migration);
            var annotated = type.IsDefined(typeof(ContractMigrationAttribute), inherit: false);

            // Una migrazione distruttiva non annotata è trattata comunque come contract: meglio fermarsi.
            return new PendingMigration(id, annotated || breaking.Count > 0, breaking);
        })];
    }

    private static string? Describe(MigrationOperation operation, HashSet<(string? Schema, string Name)> createdTables) => operation switch
    {
        DropTableOperation o => $"DropTable {o.Name}",
        DropColumnOperation o => $"DropColumn {o.Table}.{o.Name}",
        DropSchemaOperation o => $"DropSchema {o.Name}",
        RenameTableOperation o => $"RenameTable {o.Name} -> {o.NewName}",
        RenameColumnOperation o => $"RenameColumn {o.Table}.{o.Name} -> {o.NewName}",
        AlterColumnOperation o when IsNarrowing(o) => $"AlterColumn {o.Table}.{o.Name}",

        // La versione precedente non conosce la colonna: i suoi INSERT fallirebbero senza un valore predefinito.
        AddColumnOperation o when !createdTables.Contains((o.Schema, o.Table))
            && !o.IsNullable && o.DefaultValue is null && o.DefaultValueSql is null && o.ComputedColumnSql is null
            => $"AddColumn NOT NULL senza default {o.Table}.{o.Name}",

        // SQL libero: non verificabile automaticamente.
        SqlOperation => "Sql",
        _ => null,
    };

    private static bool IsNarrowing(AlterColumnOperation o) =>
        (o.OldColumn.IsNullable && !o.IsNullable)
        || o.ClrType != o.OldColumn.ClrType
        || !string.Equals(o.ColumnType, o.OldColumn.ColumnType, StringComparison.Ordinal)
        || (o.MaxLength is { } max && (o.OldColumn.MaxLength is null || max < o.OldColumn.MaxLength));
}
