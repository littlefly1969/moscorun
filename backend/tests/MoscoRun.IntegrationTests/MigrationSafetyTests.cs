using System.Reflection;
using Microsoft.EntityFrameworkCore;
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
            b.CreateTable("nuova", t => new { id = t.Column<int>(), valore = t.Column<int>(nullable: false) });
            b.AddColumn<int>("opzionale", "esistente", nullable: true);
            b.AddColumn<int>("con_default", "esistente", nullable: false, defaultValue: 0);
            b.AddColumn<int>("in_tabella_nuova", "nuova", nullable: false);
            b.CreateIndex("ix", "esistente", "opzionale");
        })).Should().BeEmpty();
    }

    [Fact]
    public void Destructive_changes_are_detected()
    {
        MigrationSafety.FindBreakingOperations(new Sample(b =>
        {
            b.DropColumn("vecchia", "esistente");
            b.DropTable("obsoleta");
            b.RenameColumn("a", "esistente", "b");
            b.RenameTable("x", newName: "y");
            b.AddColumn<int>("obbligatoria", "esistente", nullable: false);
            b.AlterColumn<string>("nome", "esistente", maxLength: 20, oldMaxLength: 80, nullable: false, oldNullable: false);
            b.AlterColumn<string>("nota", "esistente", nullable: false, oldNullable: true);
            b.Sql("UPDATE esistente SET a = 1");
        })).Should().HaveCount(8);
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
