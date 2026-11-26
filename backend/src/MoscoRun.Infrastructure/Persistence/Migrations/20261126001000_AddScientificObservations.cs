using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoscoRun.Infrastructure.Persistence.Migrations
{
    public partial class AddScientificObservations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scientific_observations",
                schema: "moscorun",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: true),
                    time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    provider_algorithm = table.Column<string>(type: "text", nullable: true),
                    derivation_version = table.Column<string>(type: "text", nullable: true),
                    input_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scientific_observations", x => new { x.owner_id, x.id });
                    table.CheckConstraint("ck_scientific_observations_time", "available_at >= time");
                    table.CheckConstraint("ck_scientific_observations_value", "value IS NULL OR (value > '-Infinity'::double precision AND value < 'Infinity'::double precision)");
                    table.ForeignKey(
                        name: "fk_scientific_observations_asp_net_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "moscorun",
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_scientific_observations_owner_id_time_available_at",
                schema: "moscorun",
                table: "scientific_observations",
                columns: new[] { "owner_id", "time", "available_at" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scientific_observations",
                schema: "moscorun");
        }
    }
}
