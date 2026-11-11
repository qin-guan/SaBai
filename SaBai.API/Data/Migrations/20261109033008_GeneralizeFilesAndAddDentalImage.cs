using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SaBai.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeFilesAndAddDentalImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ClinicalFiles becomes the shared Files table. Rename it (rather than drop and
            // recreate) so every existing patient file row is kept.
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicalFiles_Patients_PatientId",
                table: "ClinicalFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicalFiles",
                table: "ClinicalFiles");

            migrationBuilder.RenameTable(
                name: "ClinicalFiles",
                newName: "Files");

            migrationBuilder.RenameIndex(
                name: "IX_ClinicalFiles_PatientId_IsDeleted",
                table: "Files",
                newName: "IX_Files_PatientId_IsDeleted");

            migrationBuilder.AlterColumn<int>(
                name: "PatientId",
                table: "Files",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Files",
                table: "Files",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Patients_PatientId",
                table: "Files",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddColumn<int>(
                name: "ImageFileId",
                table: "DentalConsults",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DentalConsults_ImageFileId",
                table: "DentalConsults",
                column: "ImageFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_DentalConsults_Files_ImageFileId",
                table: "DentalConsults",
                column: "ImageFileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DentalConsults_Files_ImageFileId",
                table: "DentalConsults");

            migrationBuilder.DropIndex(
                name: "IX_DentalConsults_ImageFileId",
                table: "DentalConsults");

            migrationBuilder.DropColumn(
                name: "ImageFileId",
                table: "DentalConsults");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Patients_PatientId",
                table: "Files");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Files",
                table: "Files");

            // ClinicalFiles required a patient, so files without one cannot be kept.
            migrationBuilder.Sql("DELETE FROM \"Files\" WHERE \"PatientId\" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "PatientId",
                table: "Files",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.RenameTable(
                name: "Files",
                newName: "ClinicalFiles");

            migrationBuilder.RenameIndex(
                name: "IX_Files_PatientId_IsDeleted",
                table: "ClinicalFiles",
                newName: "IX_ClinicalFiles_PatientId_IsDeleted");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicalFiles",
                table: "ClinicalFiles",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalFiles_Patients_PatientId",
                table: "ClinicalFiles",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
