using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoscoRun.Infrastructure.Persistence.Migrations
{
    public partial class AddScientificSignalChunks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(name: "ix_scientific_observations_time_keyset", schema: "moscorun", table: "scientific_observations",
                columns: new[] { "owner_id", "time", "available_at", "id" });
            migrationBuilder.CreateIndex(name: "ix_scientific_observations_metric_keyset", schema: "moscorun", table: "scientific_observations",
                columns: new[] { "owner_id", "metric", "time", "available_at", "id" });

            migrationBuilder.CreateTable(
                name: "scientific_signal_chunks",
                schema: "moscorun",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    revision = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    revision_order = table.Column<long>(type: "bigint", nullable: true),
                    metric = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    provider_algorithm = table.Column<string>(type: "text", nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    workout_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_ticks = table.Column<long>(type: "bigint", nullable: false),
                    last_ticks = table.Column<long>(type: "bigint", nullable: false),
                    available_ticks = table.Column<long>(type: "bigint", nullable: false),
                    sample_count = table.Column<int>(type: "integer", nullable: false),
                    codec_version = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_hash = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scientific_signal_chunks", x => new { x.owner_id, x.id });
                    table.CheckConstraint("ck_scientific_signal_time", "first_ticks <= last_ticks AND available_ticks >= last_ticks");
                    table.CheckConstraint("ck_scientific_signal_count", "sample_count BETWEEN 1 AND 16384");
                    table.CheckConstraint("ck_scientific_signal_hash", "octet_length(content_hash) = 32");
                    table.ForeignKey(
                        name: "fk_scientific_signal_chunks_asp_net_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "moscorun",
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_scientific_signal_chunks_owner_source_metric_start",
                schema: "moscorun",
                table: "scientific_signal_chunks",
                columns: new[] { "owner_id", "source", "metric", "first_ticks", "id" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "scientific_signal_chunks", schema: "moscorun");
            migrationBuilder.DropIndex(name: "ix_scientific_observations_time_keyset", schema: "moscorun", table: "scientific_observations");
            migrationBuilder.DropIndex(name: "ix_scientific_observations_metric_keyset", schema: "moscorun", table: "scientific_observations");
        }
    }
}
