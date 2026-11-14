using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaBai.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralReimbursement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReimbursed",
                table: "Referrals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReimbursedRiels",
                table: "Referrals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReimbursedUsd",
                table: "Referrals",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReimbursedAt",
                table: "Referrals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReimbursedByUserId",
                table: "Referrals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReimbursementNotes",
                table: "Referrals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReimbursedByUserId",
                table: "Referrals",
                column: "ReimbursedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_StaffUsers_ReimbursedByUserId",
                table: "Referrals",
                column: "ReimbursedByUserId",
                principalTable: "StaffUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_StaffUsers_ReimbursedByUserId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_ReimbursedByUserId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "IsReimbursed",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "ReimbursedRiels",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "ReimbursedUsd",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "ReimbursedAt",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "ReimbursedByUserId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "ReimbursementNotes",
                table: "Referrals");
        }
    }
}
