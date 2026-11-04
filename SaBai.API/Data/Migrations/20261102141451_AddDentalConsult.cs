using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SaBai.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDentalConsult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DentalConsults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VisitId = table.Column<int>(type: "integer", nullable: false),
                    PreviousDentalVisit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PreviousDentalTreatment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Toothache = table.Column<bool>(type: "boolean", nullable: true),
                    Extraction = table.Column<bool>(type: "boolean", nullable: true),
                    DentalTrauma = table.Column<bool>(type: "boolean", nullable: true),
                    CurrentToothache = table.Column<bool>(type: "boolean", nullable: true),
                    ToothSensitivity = table.Column<bool>(type: "boolean", nullable: true),
                    BleedingGums = table.Column<bool>(type: "boolean", nullable: true),
                    BrushingFrequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    FluorideToothpaste = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SugarConsumption = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OtherDentalHistory = table.Column<string>(type: "text", nullable: true),
                    ChiefComplaint = table.Column<string>(type: "text", nullable: true),
                    FacialSymmetry = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    FacialSwelling = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Lymphadenopathy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MouthOpening = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OralHygiene = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Plaque = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Calculus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    GingivalCondition = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Caries = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MissingTeeth = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    FracturedTeeth = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MucosalLesions = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Malocclusion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Diagnoses = table.Column<List<string>>(type: "text[]", nullable: false),
                    FluorideDone = table.Column<bool>(type: "boolean", nullable: true),
                    FluorideDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FluorideOperator = table.Column<string>(type: "text", nullable: true),
                    FluorideToothNumbers = table.Column<string>(type: "text", nullable: true),
                    SealantDone = table.Column<bool>(type: "boolean", nullable: true),
                    SealantDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SealantOperator = table.Column<string>(type: "text", nullable: true),
                    SealantToothNumbers = table.Column<string>(type: "text", nullable: true),
                    SdfDone = table.Column<bool>(type: "boolean", nullable: true),
                    SdfDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SdfOperator = table.Column<string>(type: "text", nullable: true),
                    SdfToothNumbers = table.Column<string>(type: "text", nullable: true),
                    ScalingDone = table.Column<bool>(type: "boolean", nullable: true),
                    ScalingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ScalingOperator = table.Column<string>(type: "text", nullable: true),
                    ScalingToothNumbers = table.Column<string>(type: "text", nullable: true),
                    ReferredFor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReferralPriority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReferralNotes = table.Column<string>(type: "text", nullable: true),
                    RecommendedFacility = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalConsults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalConsults_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DentalConsults_VisitId",
                table: "DentalConsults",
                column: "VisitId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DentalConsults");
        }
    }
}
