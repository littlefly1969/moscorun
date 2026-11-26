using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoscoRun.Infrastructure.Persistence.Migrations
{
    public partial class AddScientificFeedbackEntries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scientific_feedback_entries",
                schema: "moscorun",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    revision = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    revision_order = table.Column<long>(type: "bigint", nullable: true),
                    time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    workout_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    observation_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scientific_feedback_entries", x => new { x.owner_id, x.id });
                    table.CheckConstraint("ck_scientific_feedback_time", "available_at >= time");
                    table.ForeignKey(
                        name: "fk_scientific_feedback_entries_asp_net_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "moscorun",
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_scientific_feedback_entries_owner_id_entry_id_available_at",
                schema: "moscorun",
                table: "scientific_feedback_entries",
                columns: new[] { "owner_id", "entry_id", "available_at" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "scientific_feedback_entries", schema: "moscorun");
        }
    }
}
