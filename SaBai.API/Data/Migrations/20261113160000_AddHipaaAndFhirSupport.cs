using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SaBai.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHipaaAndFhirSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AtcCode",
                table: "MedicationActiveIngredients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RxNormCode",
                table: "MedicationActiveIngredients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Diagnoses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodeDisplay",
                table: "Diagnoses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodingSystem",
                table: "Diagnoses",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DisclosureLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Recipient = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DisclosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisclosedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisclosureLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisclosureLogs_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    ConsentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Granted = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientConsents_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisclosureLogs_PatientId_DisclosedAt",
                table: "DisclosureLogs",
                columns: new[] { "PatientId", "DisclosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientConsents_PatientId_ConsentType",
                table: "PatientConsents",
                columns: new[] { "PatientId", "ConsentType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisclosureLogs");

            migrationBuilder.DropTable(
                name: "PatientConsents");

            migrationBuilder.DropColumn(
                name: "AtcCode",
                table: "MedicationActiveIngredients");

            migrationBuilder.DropColumn(
                name: "RxNormCode",
                table: "MedicationActiveIngredients");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Diagnoses");

            migrationBuilder.DropColumn(
                name: "CodeDisplay",
                table: "Diagnoses");

            migrationBuilder.DropColumn(
                name: "CodingSystem",
                table: "Diagnoses");
        }
    }
}
