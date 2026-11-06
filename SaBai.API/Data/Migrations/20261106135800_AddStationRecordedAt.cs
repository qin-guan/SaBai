using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaBai.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStationRecordedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecordedAt",
                table: "Vitals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecordedAt",
                table: "Eyesights",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecordedAt",
                table: "DentalConsults",
                type: "timestamp with time zone",
                nullable: true);

            // Dental consults already carry when they were last saved; use it as
            // the best available estimate. Vitals and eyesight have no timestamp.
            migrationBuilder.Sql(
                "UPDATE \"DentalConsults\" SET \"RecordedAt\" = \"UpdatedAt\" WHERE \"RecordedAt\" IS NULL;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordedAt",
                table: "Vitals");

            migrationBuilder.DropColumn(
                name: "RecordedAt",
                table: "Eyesights");

            migrationBuilder.DropColumn(
                name: "RecordedAt",
                table: "DentalConsults");
        }
    }
}
