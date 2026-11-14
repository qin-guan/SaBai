using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SaBai.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Request = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Changes = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Broadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Broadcasts", x => x.Id);
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
                name: "MedicationActiveIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UnitOfMeasurement = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FallBelow = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    RxNormCode = table.Column<string>(type: "text", nullable: true),
                    AtcCode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationActiveIngredients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferralPartners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
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
                name: "StaffUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncConflicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Table = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PreviousValue = table.Column<string>(type: "text", nullable: true),
                    IncomingValue = table.Column<string>(type: "text", nullable: true),
                    WinningValue = table.Column<string>(type: "text", nullable: true),
                    IncomingUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
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
                    Table = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRejections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VillageCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageCodes", x => x.Id);
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
                name: "MedicationBrands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ActiveIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationBrands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicationBrands_MedicationActiveIngredients_ActiveIngredie~",
                        column: x => x.ActiveIngredientId,
                        principalTable: "MedicationActiveIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    P256dhKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AuthSecret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushSubscriptions_StaffUsers_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffUserTags",
                columns: table => new
                {
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VillageCodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IdentificationNumber = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContactNo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Gender = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DrugAllergy = table.Column<string>(type: "text", nullable: false),
                    HasPoorCard = table.Column<bool>(type: "boolean", nullable: false),
                    HasBS2Card = table.Column<bool>(type: "boolean", nullable: false),
                    HasSabaiCard = table.Column<bool>(type: "boolean", nullable: false),
                    ToGetReport = table.Column<bool>(type: "boolean", nullable: false),
                    PatientImagePublicId = table.Column<string>(type: "text", nullable: true),
                    RekognitionFaceId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Patients_VillageCodes_VillageCodeId",
                        column: x => x.VillageCodeId,
                        principalTable: "VillageCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicationStocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MedicationBrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Expiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StockStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationStocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicationStocks_MedicationBrands_MedicationBrandId",
                        column: x => x.MedicationBrandId,
                        principalTable: "MedicationBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisclosureLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Files_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "PatientTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientTags", x => x.Id);
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
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Visits_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_VillageCodes_VillageCodeId",
                        column: x => x.VillageCodeId,
                        principalTable: "VillageCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StockId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PreviousValue = table.Column<string>(type: "text", nullable: false),
                    NewValue = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockChanges_MedicationStocks_StockId",
                        column: x => x.StockId,
                        principalTable: "MedicationStocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockChanges_StaffUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Consults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PastMedicalHistory = table.Column<string>(type: "text", nullable: true),
                    Consultation = table.Column<string>(type: "text", nullable: true),
                    TreatmentPlan = table.Column<string>(type: "text", nullable: true),
                    ReferredFor = table.Column<string>(type: "text", nullable: true),
                    ReferralNotes = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consults_StaffUsers_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Consults_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DentalConsults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    ImageFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalConsults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalConsults_Files_ImageFileId",
                        column: x => x.ImageFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DentalConsults_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Eyesights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeftEyeDegree = table.Column<string>(type: "text", nullable: true),
                    RightEyeDegree = table.Column<string>(type: "text", nullable: true),
                    LeftEyePinhole = table.Column<string>(type: "text", nullable: true),
                    RightEyePinhole = table.Column<string>(type: "text", nullable: true),
                    LeftAstigmatism = table.Column<string>(type: "text", nullable: true),
                    RightAstigmatism = table.Column<string>(type: "text", nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    LeftPrescribedGlassesDegree = table.Column<string>(type: "text", nullable: true),
                    RightPrescribedGlassesDegree = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Eyesights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Eyesights_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vitals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Height = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Temperature = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    Systolic = table.Column<int>(type: "integer", nullable: true),
                    Diastolic = table.Column<int>(type: "integer", nullable: true),
                    HeartRate = table.Column<int>(type: "integer", nullable: true),
                    HemocueCount = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    DiabetesMellitus = table.Column<bool>(type: "boolean", nullable: true),
                    UrineTest = table.Column<string>(type: "text", nullable: true),
                    BloodGlucoseNonFasting = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    BloodGlucoseFasting = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Hba1c = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Others = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vitals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vitals_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Diagnoses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CodingSystem = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: true),
                    CodeDisplay = table.Column<string>(type: "text", nullable: true),
                    ConsultId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diagnoses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Diagnoses_Consults_ConsultId",
                        column: x => x.ConsultId,
                        principalTable: "Consults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsultId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicationBrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    DosageInstructions = table.Column<string>(type: "text", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("ck_orders_quantity_positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_Orders_Consults_ConsultId",
                        column: x => x.ConsultId,
                        principalTable: "Consults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_MedicationBrands_MedicationBrandId",
                        column: x => x.MedicationBrandId,
                        principalTable: "MedicationBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_StaffUsers_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Referrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReferredFor = table.Column<string>(type: "text", nullable: false),
                    ReferralNotes = table.Column<string>(type: "text", nullable: true),
                    ReferralState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReferralOutcome = table.Column<string>(type: "text", nullable: true),
                    ConsultId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReferredToId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberInChargeId = table.Column<Guid>(type: "uuid", nullable: true),
                    GuardiansContacted = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ClassOfService = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DescriptionOfService = table.Column<string>(type: "text", nullable: true),
                    PatientSummaryFromScreening = table.Column<string>(type: "text", nullable: true),
                    RemarksAtBooth = table.Column<string>(type: "text", nullable: true),
                    MemberOnRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    PresentAtReferralParty = table.Column<bool>(type: "boolean", nullable: false),
                    ReferralPartyResults = table.Column<string>(type: "text", nullable: true),
                    FollowUp = table.Column<bool>(type: "boolean", nullable: false),
                    DoctorsUpdated = table.Column<bool>(type: "boolean", nullable: false),
                    AmountPaidUsd = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    AmountPaidRiels = table.Column<int>(type: "integer", nullable: true),
                    MemberWhoPaidId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Referrals_Consults_ConsultId",
                        column: x => x.ConsultId,
                        principalTable: "Consults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Referrals_ReferralPartners_ReferredToId",
                        column: x => x.ReferredToId,
                        principalTable: "ReferralPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Referrals_StaffUsers_MemberInChargeId",
                        column: x => x.MemberInChargeId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Referrals_StaffUsers_MemberOnRunId",
                        column: x => x.MemberOnRunId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Referrals_StaffUsers_MemberWhoPaidId",
                        column: x => x.MemberWhoPaidId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChildVitals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VitalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossMotor = table.Column<string>(type: "text", nullable: true),
                    RedReflex = table.Column<string>(type: "text", nullable: true),
                    Scoliosis = table.Column<string>(type: "text", nullable: true),
                    Pallor = table.Column<bool>(type: "boolean", nullable: true),
                    OralCavity = table.Column<string>(type: "text", nullable: true),
                    Heart = table.Column<string>(type: "text", nullable: true),
                    Abdomen = table.Column<string>(type: "text", nullable: true),
                    Lungs = table.Column<string>(type: "text", nullable: true),
                    HernialOrifices = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildVitals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildVitals_Vitals_VitalsId",
                        column: x => x.VitalsId,
                        principalTable: "Vitals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PubertyAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VitalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pubarche = table.Column<bool>(type: "boolean", nullable: true),
                    PubarcheAge = table.Column<int>(type: "integer", nullable: true),
                    Thelarche = table.Column<bool>(type: "boolean", nullable: true),
                    ThelarcheAge = table.Column<int>(type: "integer", nullable: true),
                    Menarche = table.Column<bool>(type: "boolean", nullable: true),
                    MenarcheAge = table.Column<int>(type: "integer", nullable: true),
                    VoiceChange = table.Column<bool>(type: "boolean", nullable: true),
                    VoiceChangeAge = table.Column<int>(type: "integer", nullable: true),
                    TesticularGrowth = table.Column<bool>(type: "boolean", nullable: true),
                    TesticularGrowthAge = table.Column<int>(type: "integer", nullable: true),
                    AdditionalNotes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PubertyAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PubertyAssessments_Vitals_VitalsId",
                        column: x => x.VitalsId,
                        principalTable: "Vitals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockChangeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => new { x.OrderId, x.StockChangeId });
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_StockChanges_StockChangeId",
                        column: x => x.StockChangeId,
                        principalTable: "StockChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAt",
                table: "AuditLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildVitals_VitalsId",
                table: "ChildVitals",
                column: "VitalsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consults_DoctorId",
                table: "Consults",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Consults_VisitId",
                table: "Consults",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalConsults_ImageFileId",
                table: "DentalConsults",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalConsults_VisitId",
                table: "DentalConsults",
                column: "VisitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_ConsultId",
                table: "Diagnoses",
                column: "ConsultId");

            migrationBuilder.CreateIndex(
                name: "IX_DisclosureLogs_PatientId_DisclosedAt",
                table: "DisclosureLogs",
                columns: new[] { "PatientId", "DisclosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Eyesights_VisitId",
                table: "Eyesights",
                column: "VisitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_PatientId_IsDeleted",
                table: "Files",
                columns: new[] { "PatientId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicationBrands_ActiveIngredientId",
                table: "MedicationBrands",
                column: "ActiveIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicationStocks_MedicationBrandId",
                table: "MedicationStocks",
                column: "MedicationBrandId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicationStocks_StockStatus",
                table: "MedicationStocks",
                column: "StockStatus");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_StockChangeId",
                table: "OrderItems",
                column: "StockChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ApprovedById",
                table: "Orders",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ConsultId",
                table: "Orders",
                column: "ConsultId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MedicationBrandId",
                table: "Orders",
                column: "MedicationBrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PatientConsents_PatientId_ConsentType",
                table: "PatientConsents",
                columns: new[] { "PatientId", "ConsentType" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_IdentificationNumber",
                table: "Patients",
                column: "IdentificationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Name",
                table: "Patients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Number",
                table: "Patients",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_VillageCodeId",
                table: "Patients",
                column: "VillageCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTags_PatientId_TagId",
                table: "PatientTags",
                columns: new[] { "PatientId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientTags_TagId",
                table: "PatientTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_PubertyAssessments_VitalsId",
                table: "PubertyAssessments",
                column: "VitalsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_StaffUserId",
                table: "PushSubscriptions",
                column: "StaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ConsultId",
                table: "Referrals",
                column: "ConsultId");

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
                name: "IX_StaffUserTags_TagId",
                table: "StaffUserTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_StockChanges_StockId",
                table: "StockChanges",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_StockChanges_UserId",
                table: "StockChanges",
                column: "UserId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NormalizedName",
                table: "Tags",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageCodes_Code",
                table: "VillageCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visits_Date",
                table: "Visits",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_PatientId",
                table: "Visits",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_VillageCodeId",
                table: "Visits",
                column: "VillageCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Vitals_VisitId",
                table: "Vitals",
                column: "VisitId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Broadcasts");

            migrationBuilder.DropTable(
                name: "ChildVitals");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "DentalConsults");

            migrationBuilder.DropTable(
                name: "Diagnoses");

            migrationBuilder.DropTable(
                name: "DisclosureLogs");

            migrationBuilder.DropTable(
                name: "Eyesights");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "PatientConsents");

            migrationBuilder.DropTable(
                name: "PatientTags");

            migrationBuilder.DropTable(
                name: "PubertyAssessments");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropTable(
                name: "Referrals");

            migrationBuilder.DropTable(
                name: "StaffUserTags");

            migrationBuilder.DropTable(
                name: "SyncConflicts");

            migrationBuilder.DropTable(
                name: "SyncRejections");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Files");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "StockChanges");

            migrationBuilder.DropTable(
                name: "Vitals");

            migrationBuilder.DropTable(
                name: "ReferralPartners");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Consults");

            migrationBuilder.DropTable(
                name: "MedicationStocks");

            migrationBuilder.DropTable(
                name: "StaffUsers");

            migrationBuilder.DropTable(
                name: "Visits");

            migrationBuilder.DropTable(
                name: "MedicationBrands");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "MedicationActiveIngredients");

            migrationBuilder.DropTable(
                name: "VillageCodes");
        }
    }
}
