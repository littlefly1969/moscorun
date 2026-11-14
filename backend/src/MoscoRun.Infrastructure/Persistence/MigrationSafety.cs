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

    /// <summary>
    /// Elenco chiuso (fail closed): sono compatibili solo le operazioni puramente additive riconosciute qui.
    /// Qualsiasi altra operazione, compresi tipi futuri o sconosciuti, è considerata contract finché una persona
    /// non la dichiara tale con <see cref="ContractMigrationAttribute"/>.
    /// </summary>
    private static string? Describe(MigrationOperation operation, HashSet<(string? Schema, string Name)> createdTables)
    {
        bool IsNew(string? schema, string table) => createdTables.Contains((schema, table));

        return operation switch
        {
            EnsureSchemaOperation or CreateTableOperation or CreateSequenceOperation => null,

            // Un indice normale non cambia il comportamento delle scritture; un indice UNIQUE su una tabella
            // esistente può far fallire INSERT/UPDATE che la versione precedente considera validi.
            CreateIndexOperation o when !o.IsUnique || IsNew(o.Schema, o.Table) => null,
            CreateIndexOperation o => $"CreateIndex UNIQUE {o.Table}.{o.Name}",

            // Nuovi vincoli su tabelle esistenti possono rifiutare scritture della versione precedente.
            AddForeignKeyOperation o when IsNew(o.Schema, o.Table) => null,
            AddForeignKeyOperation o => $"AddForeignKey {o.Table}.{o.Name}",
            AddCheckConstraintOperation o when IsNew(o.Schema, o.Table) => null,
            AddCheckConstraintOperation o => $"AddCheckConstraint {o.Table}.{o.Name}",
            AddUniqueConstraintOperation o when IsNew(o.Schema, o.Table) => null,
            AddUniqueConstraintOperation o => $"AddUniqueConstraint {o.Table}.{o.Name}",
            AddPrimaryKeyOperation o when IsNew(o.Schema, o.Table) => null,
            AddPrimaryKeyOperation o => $"AddPrimaryKey {o.Table}.{o.Name}",

            // La versione precedente non conosce la colonna: i suoi INSERT riescono solo se può restare vuota
            // o ha un valore predefinito.
            AddColumnOperation o when IsNew(o.Schema, o.Table)
                || (o.ComputedColumnSql is null && (o.IsNullable || o.DefaultValue is not null || o.DefaultValueSql is not null)) => null,
            AddColumnOperation o => $"AddColumn {o.Table}.{o.Name} (NOT NULL senza default o calcolata)",

            AlterColumnOperation o when IsWidening(o) => null,
            AlterColumnOperation o => $"AlterColumn {o.Table}.{o.Name}",

            _ => $"{operation.GetType().Name.Replace("Operation", string.Empty, StringComparison.Ordinal)} {Target(operation)}".TrimEnd(),
        };
    }

    /// <summary>Solo allargamenti: stessa colonna, stesso tipo, vincoli uguali o più permissivi, default conservato.</summary>
    private static bool IsWidening(AlterColumnOperation o)
    {
        var old = o.OldColumn;
        var sameType = o.ClrType == old.ClrType && string.Equals(o.ColumnType, old.ColumnType, StringComparison.Ordinal);
        var nullability = o.IsNullable || !old.IsNullable;
        var length = o.MaxLength is null || (old.MaxLength is { } oldMax && o.MaxLength >= oldMax);
        var oldHasDefault = old.DefaultValue is not null || old.DefaultValueSql is not null;
        var newHasDefault = o.DefaultValue is not null || o.DefaultValueSql is not null;
        var keepsDefault = !oldHasDefault || newHasDefault || o.IsNullable;
        var computed = string.Equals(o.ComputedColumnSql, old.ComputedColumnSql, StringComparison.Ordinal);
        return sameType && nullability && length && keepsDefault && computed;
    }

    private static string Target(MigrationOperation operation) => operation switch
    {
        DropTableOperation o => o.Name,
        DropColumnOperation o => $"{o.Table}.{o.Name}",
        DropIndexOperation o => $"{o.Table}.{o.Name}",
        DropForeignKeyOperation o => $"{o.Table}.{o.Name}",
        DropSchemaOperation o => o.Name,
        RenameTableOperation o => $"{o.Name} -> {o.NewName}",
        RenameColumnOperation o => $"{o.Table}.{o.Name} -> {o.NewName}",
        RenameIndexOperation o => $"{o.Name} -> {o.NewName}",
        _ => string.Empty,
    };
}
