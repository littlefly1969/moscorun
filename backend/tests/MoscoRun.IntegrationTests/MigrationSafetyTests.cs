using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Api.Commands;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

/// <summary>
/// Regola expand/contract: il rollback automatico riporta l'immagine precedente ma non lo schema,
/// quindi una migrazione distruttiva deve essere dichiarata e non può essere applicata in automatico.
/// </summary>
public sealed class MigrationSafetyTests(ApiFactory factory)
{
    [Fact]
    public async Task Every_destructive_migration_is_declared_as_contract()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>();
        var assembly = db.GetService<IMigrationsAssembly>();

        assembly.Migrations.Should().NotBeEmpty();
        foreach (var (id, type) in assembly.Migrations)
        {
            var breaking = MigrationSafety.FindBreakingOperations(assembly.CreateMigration(type, db.Database.ProviderName!));
            if (breaking.Count > 0)
            {
                type.GetCustomAttribute<ContractMigrationAttribute>().Should().NotBeNull(
                    $"{id} contiene {string.Join(", ", breaking)}: dividerla in expand + contract o annotarla con [ContractMigration]");
            }
        }
    }

    [Fact]
    public void Additive_changes_are_compatible()
    {
        MigrationSafety.FindBreakingOperations(new Sample(b =>
        {
            b.EnsureSchema("moscorun");
            b.CreateTable("nuova", t => new { id = t.Column<int>(), valore = t.Column<int>(nullable: false) });
            b.AddPrimaryKey("pk_nuova", "nuova", "id");
            b.AddForeignKey("fk_nuova", "nuova", "valore", "esistente");
            b.AddCheckConstraint("ck_nuova", "nuova", "valore > 0");
            b.AddUniqueConstraint("uq_nuova", "nuova", "valore");
            b.CreateIndex("ux_nuova", "nuova", "valore", unique: true);
            b.AddColumn<int>("in_tabella_nuova", "nuova", nullable: false);
            b.AddColumn<int>("opzionale", "esistente", nullable: true);
            b.AddColumn<int>("con_default", "esistente", nullable: false, defaultValue: 0);
            b.CreateIndex("ix", "esistente", "opzionale");
            b.AlterColumn<string>("nome", "esistente", maxLength: 120, oldMaxLength: 80, nullable: true, oldNullable: false);
        })).Should().BeEmpty();
    }

    /// <summary>Ogni caso è una modifica che può rompere letture o scritture della versione precedente.</summary>
    public static TheoryData<string, Action<MigrationBuilder>> BreakingCases => new()
    {
        { "DropColumn", b => b.DropColumn("vecchia", "esistente") },
        { "DropTable", b => b.DropTable("obsoleta") },
        { "DropIndex", b => b.DropIndex("ux_idempotenza", "esistente") },
        { "DropForeignKey", b => b.DropForeignKey("fk", "esistente") },
        { "RenameColumn", b => b.RenameColumn("a", "esistente", "b") },
        { "RenameTable", b => b.RenameTable("x", newName: "y") },
        { "AddColumn NOT NULL", b => b.AddColumn<int>("obbligatoria", "esistente", nullable: false) },
        { "AddColumn calcolata", b => b.AddColumn<int>("calcolata", "esistente", nullable: true, computedColumnSql: "a + 1") },
        { "CreateIndex UNIQUE", b => b.CreateIndex("ux_nuovo", "esistente", "codice", unique: true) },
        { "AddForeignKey", b => b.AddForeignKey("fk_nuova", "esistente", "altra_id", "altra") },
        { "AddCheckConstraint", b => b.AddCheckConstraint("ck_nuovo", "esistente", "peso > 0") },
        { "AddUniqueConstraint", b => b.AddUniqueConstraint("uq_nuovo", "esistente", "codice") },
        { "AlterColumn più corta", b => b.AlterColumn<string>("nome", "esistente", maxLength: 20, oldMaxLength: 80) },
        { "AlterColumn NOT NULL", b => b.AlterColumn<string>("nota", "esistente", nullable: false, oldNullable: true) },
        { "AlterColumn tipo", b => b.AlterColumn<long>("n", "esistente", oldClrType: typeof(int)) },
        { "AlterColumn senza default", b => b.AlterColumn<int>("stato", "esistente", nullable: false, oldNullable: false, oldDefaultValue: 0) },
        { "Sql", b => b.Sql("UPDATE esistente SET a = 1") },
        { "InsertData", b => b.InsertData("esistente", "id", 1) },
        { "AlterDatabase", b => b.AlterDatabase() },
    };

    [Theory]
    [MemberData(nameof(BreakingCases))]
    public void Anything_not_known_to_be_additive_is_breaking(string name, Action<MigrationBuilder> up)
    {
        MigrationSafety.FindBreakingOperations(new Sample(up)).Should().ContainSingle(name);
    }

    [Fact]
    public async Task Migrate_plan_reports_an_up_to_date_database_without_changes()
    {
        (await MigrateCommand.RunAsync(factory.Services, ["migrate", "--plan"])).Should().Be(0);
    }

    private sealed class Sample(Action<MigrationBuilder> up) : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) => up(migrationBuilder);
    }
}
