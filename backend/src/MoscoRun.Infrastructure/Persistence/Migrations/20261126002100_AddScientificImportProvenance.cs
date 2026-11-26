using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoscoRun.Infrastructure.Persistence.Migrations
{
    public partial class AddScientificImportProvenance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(name: "import_device_id", schema: "moscorun", table: "scientific_observations",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "workout_id", schema: "moscorun", table: "scientific_observations",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<string>(name: "native_metric", schema: "moscorun", table: "scientific_observations",
                type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "native_unit", schema: "moscorun", table: "scientific_observations",
                type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<double>(name: "native_value", schema: "moscorun", table: "scientific_observations",
                type: "double precision", nullable: true);
            migrationBuilder.AddColumn<string>(name: "mapping_version", schema: "moscorun", table: "scientific_observations",
                type: "character varying(200)", maxLength: 200, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "import_device_id", schema: "moscorun", table: "scientific_observations");
            migrationBuilder.DropColumn(name: "workout_id", schema: "moscorun", table: "scientific_observations");
            migrationBuilder.DropColumn(name: "native_metric", schema: "moscorun", table: "scientific_observations");
            migrationBuilder.DropColumn(name: "native_unit", schema: "moscorun", table: "scientific_observations");
            migrationBuilder.DropColumn(name: "native_value", schema: "moscorun", table: "scientific_observations");
            migrationBuilder.DropColumn(name: "mapping_version", schema: "moscorun", table: "scientific_observations");
        }
    }
}
