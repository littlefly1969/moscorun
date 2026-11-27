using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoscoRun.Infrastructure.Persistence.Migrations
{
    public partial class AddScienceDiagnostics : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "science_diagnostics",
                schema: "moscorun",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_science_diagnostics", x => new { x.owner_id, x.device_id, x.source });
                    table.ForeignKey(
                        name: "fk_science_diagnostics_asp_net_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "moscorun",
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "science_diagnostics", schema: "moscorun");
        }
    }
}
