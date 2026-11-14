using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaBai.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePowerSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Databases provisioned for the offline replica have a logical replication
            // publication over the clinic tables; nothing reads it any more.
            migrationBuilder.Sql("DROP PUBLICATION IF EXISTS powersync;");

            migrationBuilder.DropTable(
                name: "SyncConflicts");

            migrationBuilder.DropTable(
                name: "SyncRejections");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PatientTags",
                table: "PatientTags");

            migrationBuilder.DropIndex(
                name: "IX_PatientTags_PatientId_TagId",
                table: "PatientTags");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Vitals");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "VillageCodes");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "StockChanges");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ReferralPartners");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PubertyAssessments");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "PatientTags");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "MedicationStocks");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "MedicationBrands");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "MedicationActiveIngredients");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Eyesights");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Diagnoses");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "DentalConsults");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Consults");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ChildVitals");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Vitals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Visits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "VillageCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "StockChanges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Referrals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "ReferralPartners",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "PubertyAssessments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Patients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "MedicationStocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "MedicationBrands",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "MedicationActiveIngredients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Files",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Eyesights",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Diagnoses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "DentalConsults",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Consults",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "ChildVitals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PatientTags",
                table: "PatientTags",
                columns: new[] { "PatientId", "TagId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PatientTags",
                table: "PatientTags");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Vitals");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "VillageCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "StockChanges");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "ReferralPartners");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "PubertyAssessments");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "MedicationStocks");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "MedicationBrands");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "MedicationActiveIngredients");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Eyesights");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Diagnoses");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "DentalConsults");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Consults");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "ChildVitals");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Vitals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Visits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "VillageCodes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "StockChanges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Referrals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "ReferralPartners",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "PubertyAssessments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing links each need their own key before it can become the primary key.
            migrationBuilder.Sql(
                "ALTER TABLE \"PatientTags\" ADD \"Id\" uuid NOT NULL DEFAULT gen_random_uuid();"
            );
            migrationBuilder.Sql("ALTER TABLE \"PatientTags\" ALTER COLUMN \"Id\" DROP DEFAULT;");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Patients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "MedicationStocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "MedicationBrands",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "MedicationActiveIngredients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Files",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Eyesights",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Diagnoses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "DentalConsults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Consults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "ChildVitals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PatientTags",
                table: "PatientTags",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "SyncConflicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IncomingUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncomingValue = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousValue = table.Column<string>(type: "text", nullable: true),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Table = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WinningValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncConflicts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncRejections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Table = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRejections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientTags_PatientId_TagId",
                table: "PatientTags",
                columns: new[] { "PatientId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncConflicts_ResolvedAt",
                table: "SyncConflicts",
                column: "ResolvedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncConflicts_Table_RecordId",
                table: "SyncConflicts",
                columns: new[] { "Table", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncRejections_ResolvedAt",
                table: "SyncRejections",
                column: "ResolvedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRejections_Table_RecordId",
                table: "SyncRejections",
                columns: new[] { "Table", "RecordId" });
        }
    }
}
