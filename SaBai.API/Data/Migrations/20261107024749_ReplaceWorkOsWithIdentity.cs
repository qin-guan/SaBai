using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SaBai.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceWorkOsWithIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffUsers_Email",
                table: "StaffUsers");

            migrationBuilder.DropIndex(
                name: "IX_StaffUsers_Username",
                table: "StaffUsers");

            migrationBuilder.DropIndex(
                name: "IX_StaffUsers_WorkOsUserId",
                table: "StaffUsers");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "StaffUsers",
                newName: "UserName");

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "StaffUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "StaffUsers",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "StaffUsers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "StaffUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "StaffUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "StaffUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LockoutEnabled",
                table: "StaffUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockoutEnd",
                table: "StaffUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "StaffUsers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUserName",
                table: "StaffUsers",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "StaffUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "StaffUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PhoneNumberConfirmed",
                table: "StaffUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "StaffUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_StaffUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_StaffUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_StaffUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_StaffUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Carry the WorkOS-era staff rows over to Identity. Passwords lived in WorkOS
            // and are not migrated: these accounts have no password until an admin sets
            // one (or Auth:BootstrapAdmin does at startup).
            migrationBuilder.Sql(
                """
                UPDATE "StaffUsers" SET
                    "NormalizedUserName" = upper("UserName"),
                    "NormalizedEmail" = upper("Email"),
                    "EmailConfirmed" = TRUE,
                    "SecurityStamp" = upper(replace(gen_random_uuid()::text, '-', '')),
                    "ConcurrencyStamp" = gen_random_uuid()::text,
                    "LockoutEnabled" = TRUE,
                    "LockoutEnd" = CASE WHEN "IsLocked" THEN 'infinity'::timestamptz ELSE NULL END;

                INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
                VALUES
                    ('a7d1f0c2-6b1e-4d3a-9f53-0c6e2b8d1a01', 'admin', 'ADMIN', 'a7d1f0c2-6b1e-4d3a-9f53-0c6e2b8d1a01'),
                    ('a7d1f0c2-6b1e-4d3a-9f53-0c6e2b8d1a02', 'member', 'MEMBER', 'a7d1f0c2-6b1e-4d3a-9f53-0c6e2b8d1a02');

                INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
                SELECT u."Id", r."Id"
                FROM "StaffUsers" u
                JOIN "AspNetRoles" r ON r."NormalizedName" = upper(u."Role");
                """
            );

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "WorkOsUserId",
                table: "StaffUsers");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "StaffUsers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "StaffUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "StaffUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "StaffUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Member");

            migrationBuilder.AddColumn<string>(
                name: "WorkOsUserId",
                table: "StaffUsers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            // WorkOS ids cannot be recovered; staff must be re-linked by email.
            migrationBuilder.Sql(
                """
                UPDATE "StaffUsers" u SET
                    "IsLocked" = COALESCE(u."LockoutEnd" > now(), FALSE),
                    "WorkOsUserId" = 'unlinked_' || u."Id"::text,
                    "Email" = COALESCE(u."Email", u."UserName" || '@unknown.invalid'),
                    "Role" = CASE WHEN EXISTS (
                        SELECT 1 FROM "AspNetUserRoles" ur
                        JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                        WHERE ur."UserId" = u."Id" AND r."NormalizedName" = 'ADMIN'
                    ) THEN 'Admin' ELSE 'Member' END;
                """
            );

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "StaffUsers");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "LockoutEnabled",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "NormalizedUserName",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "PhoneNumberConfirmed",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "StaffUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "StaffUsers");

            migrationBuilder.RenameColumn(
                name: "UserName",
                table: "StaffUsers",
                newName: "Username");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "StaffUsers",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "StaffUsers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffUsers_Email",
                table: "StaffUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffUsers_Username",
                table: "StaffUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffUsers_WorkOsUserId",
                table: "StaffUsers",
                column: "WorkOsUserId",
                unique: true);
        }
    }
}
