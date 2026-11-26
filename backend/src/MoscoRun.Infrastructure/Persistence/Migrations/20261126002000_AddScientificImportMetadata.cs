using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoscoRun.Infrastructure.Persistence.Migrations
{
    public partial class AddScientificImportMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "external_id", schema: "moscorun", table: "scientific_observations",
                type: "character varying(512)", maxLength: 512, nullable: true);
            migrationBuilder.AddColumn<string>(name: "external_revision", schema: "moscorun", table: "scientific_observations",
                type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<long>(name: "external_revision_order", schema: "moscorun", table: "scientific_observations",
                type: "bigint", nullable: true);
            migrationBuilder.CreateIndex(name: "ix_scientific_observations_external_lookup", schema: "moscorun",
                table: "scientific_observations", columns: new[] { "owner_id", "source", "external_id", "metric" });
            migrationBuilder.CreateTable(
                name: "scientific_sync_checkpoints",
                schema: "moscorun",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    stream = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cursor = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scientific_sync_checkpoints", x => new { x.owner_id, x.source, x.stream });
                    table.ForeignKey(
                        name: "fk_scientific_sync_checkpoints_asp_net_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "moscorun",
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "scientific_sync_checkpoints", schema: "moscorun");
            migrationBuilder.DropIndex(name: "ix_scientific_observations_external_lookup", schema: "moscorun", table: "scientific_observations");
            migrationBuilder.DropColumn(name: "external_id", schema: "moscorun", table: "scientific_observations");
            migrationBuilder.DropColumn(name: "external_revision", schema: "moscorun", table: "scientific_observations");
            migrationBuilder.DropColumn(name: "external_revision_order", schema: "moscorun", table: "scientific_observations");
        }
    }
}
