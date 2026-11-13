using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SaBai.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralsOverhaulAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AmountPaidRiels",
                table: "Referrals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaidUsd",
                table: "Referrals",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassOfService",
                table: "Referrals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionOfService",
                table: "Referrals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DoctorsUpdated",
                table: "Referrals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FollowUp",
                table: "Referrals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GuardiansContacted",
                table: "Referrals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MemberInChargeId",
                table: "Referrals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MemberOnRunId",
                table: "Referrals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MemberWhoPaidId",
                table: "Referrals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientSummaryFromScreening",
                table: "Referrals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PresentAtReferralParty",
                table: "Referrals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferralPartyResults",
                table: "Referrals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredToId",
                table: "Referrals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemarksAtBooth",
                table: "Referrals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Referrals",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "ReferralPartners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralPartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientTags",
                columns: table => new
                {
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    TagId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientTags", x => new { x.PatientId, x.TagId });
                    table.ForeignKey(
                        name: "FK_PatientTags_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PatientTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffUserTags",
                columns: table => new
                {
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffUserTags", x => new { x.StaffUserId, x.TagId });
                    table.ForeignKey(
                        name: "FK_StaffUserTags_StaffUsers_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffUserTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_MemberInChargeId",
                table: "Referrals",
                column: "MemberInChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_MemberOnRunId",
                table: "Referrals",
                column: "MemberOnRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_MemberWhoPaidId",
                table: "Referrals",
                column: "MemberWhoPaidId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferredToId",
                table: "Referrals",
                column: "ReferredToId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTags_TagId",
                table: "PatientTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffUserTags_TagId",
                table: "StaffUserTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NormalizedName",
                table: "Tags",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_ReferralPartners_ReferredToId",
                table: "Referrals",
                column: "ReferredToId",
                principalTable: "ReferralPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_StaffUsers_MemberInChargeId",
                table: "Referrals",
                column: "MemberInChargeId",
                principalTable: "StaffUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_StaffUsers_MemberOnRunId",
                table: "Referrals",
                column: "MemberOnRunId",
                principalTable: "StaffUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_StaffUsers_MemberWhoPaidId",
                table: "Referrals",
                column: "MemberWhoPaidId",
                principalTable: "StaffUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_ReferralPartners_ReferredToId",
                table: "Referrals");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_StaffUsers_MemberInChargeId",
                table: "Referrals");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_StaffUsers_MemberOnRunId",
                table: "Referrals");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_StaffUsers_MemberWhoPaidId",
                table: "Referrals");

            migrationBuilder.DropTable(
                name: "PatientTags");

            migrationBuilder.DropTable(
                name: "ReferralPartners");

            migrationBuilder.DropTable(
                name: "StaffUserTags");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_MemberInChargeId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_MemberOnRunId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_MemberWhoPaidId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_ReferredToId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "AmountPaidRiels",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "AmountPaidUsd",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "ClassOfService",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "DescriptionOfService",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "DoctorsUpdated",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "FollowUp",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "GuardiansContacted",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "MemberInChargeId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "MemberOnRunId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "MemberWhoPaidId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "PatientSummaryFromScreening",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "PresentAtReferralParty",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "ReferralPartyResults",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "ReferredToId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "RemarksAtBooth",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Referrals");
        }
    }
}
