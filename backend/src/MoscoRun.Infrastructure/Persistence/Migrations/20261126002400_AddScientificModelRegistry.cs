using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoscoRun.Infrastructure.Persistence.Migrations
{
    public partial class AddScientificModelRegistry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scientific_model_artifacts",
                schema: "moscorun",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    format = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    artifact = table.Column<byte[]>(type: "bytea", nullable: false),
                    artifact_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    feature_schema_version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    training_cutoff = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    validation_json = table.Column<string>(type: "jsonb", nullable: false),
                    configuration_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scientific_model_artifacts", x => new { x.owner_id, x.model_id, x.version });
                    table.CheckConstraint("ck_scientific_model_artifact_hash", "octet_length(artifact_hash) = 32");
                    table.CheckConstraint("ck_scientific_model_artifact_size", "octet_length(artifact) BETWEEN 1 AND 16777216");
                    table.CheckConstraint("ck_scientific_model_training_time", "training_cutoff <= registered_at");
                    table.ForeignKey(
                        name: "fk_scientific_model_artifacts_asp_net_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "moscorun",
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scientific_model_runs",
                schema: "moscorun",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    decision_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    target_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    input_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    output_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    outputs_json = table.Column<string>(type: "jsonb", nullable: false),
                    state_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scientific_model_runs", x => new { x.owner_id, x.id });
                    table.CheckConstraint("ck_scientific_model_run_time", "available_at >= decision_at AND (target_at IS NULL OR target_at >= decision_at)");
                    table.ForeignKey(
                        name: "fk_scientific_model_runs_scientific_model_artifacts_model",
                        columns: x => new { x.owner_id, x.model_id, x.version },
                        principalSchema: "moscorun",
                        principalTable: "scientific_model_artifacts",
                        principalColumns: new[] { "owner_id", "model_id", "version" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "ix_scientific_model_runs_state_lookup", schema: "moscorun", table: "scientific_model_runs",
                columns: new[] { "owner_id", "model_id", "version", "decision_at", "available_at" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "scientific_model_runs", schema: "moscorun");
            migrationBuilder.DropTable(name: "scientific_model_artifacts", schema: "moscorun");
        }
    }
}
