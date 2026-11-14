using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoscoRun.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workouts",
                schema: "moscorun",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    activity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    utc_offset_minutes = table.Column<int>(type: "integer", nullable: true),
                    active_duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    distance_m = table.Column<double>(type: "double precision", nullable: true),
                    energy_kcal = table.Column<double>(type: "double precision", nullable: true),
                    average_heart_rate = table.Column<double>(type: "double precision", nullable: true),
                    max_heart_rate = table.Column<double>(type: "double precision", nullable: true),
                    track_point_count = table.Column<int>(type: "integer", nullable: false),
                    min_latitude = table.Column<double>(type: "double precision", nullable: true),
                    min_longitude = table.Column<double>(type: "double precision", nullable: true),
                    max_latitude = table.Column<double>(type: "double precision", nullable: true),
                    max_longitude = table.Column<double>(type: "double precision", nullable: true),
                    content_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workouts", x => x.id);
                    table.CheckConstraint("ck_workouts_content_hash", "octet_length(content_hash) = 32");
                    table.CheckConstraint("ck_workouts_distance_m", "distance_m IS NULL OR distance_m BETWEEN 0 AND 1000000");
                    table.CheckConstraint("ck_workouts_time", "ended_at > started_at");
                    table.CheckConstraint("ck_workouts_track_point_count", "track_point_count BETWEEN 0 AND 100000");
                    table.ForeignKey(
                        name: "fk_workouts_asp_net_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "moscorun",
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workouts_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "moscorun",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workout_routes",
                schema: "moscorun",
                columns: table => new
                {
                    workout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    point_count = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workout_routes", x => x.workout_id);
                    table.ForeignKey(
                        name: "fk_workout_routes_workouts_workout_id",
                        column: x => x.workout_id,
                        principalSchema: "moscorun",
                        principalTable: "workouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_series",
                schema: "moscorun",
                columns: table => new
                {
                    workout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sample_count = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workout_series", x => new { x.workout_id, x.metric });
                    table.ForeignKey(
                        name: "fk_workout_series_workouts_workout_id",
                        column: x => x.workout_id,
                        principalSchema: "moscorun",
                        principalTable: "workouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workouts_device_id",
                schema: "moscorun",
                table: "workouts",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_workouts_owner_id_source_source_id",
                schema: "moscorun",
                table: "workouts",
                columns: new[] { "owner_id", "source", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workouts_owner_id_started_at",
                schema: "moscorun",
                table: "workouts",
                columns: new[] { "owner_id", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workout_routes",
                schema: "moscorun");

            migrationBuilder.DropTable(
                name: "workout_series",
                schema: "moscorun");

            migrationBuilder.DropTable(
                name: "workouts",
                schema: "moscorun");
        }
    }
}
