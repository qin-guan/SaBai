using System;
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
                name: "Broadcasts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
                name: "MedicationActiveIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UnitOfMeasurement = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FallBelow = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationActiveIngredients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOsUserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Nickname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VillageCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
                name: "MedicationBrands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ActiveIngredientId = table.Column<int>(type: "integer", nullable: false),
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
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VillageCodeId = table.Column<int>(type: "integer", nullable: true),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicationBrandId = table.Column<int>(type: "integer", nullable: false),
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
                name: "ClinicalFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalFiles_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    VillageCodeId = table.Column<int>(type: "integer", nullable: false),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockId = table.Column<int>(type: "integer", nullable: false),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PastMedicalHistory = table.Column<string>(type: "text", nullable: true),
                    Consultation = table.Column<string>(type: "text", nullable: true),
                    TreatmentPlan = table.Column<string>(type: "text", nullable: true),
                    ReferredFor = table.Column<string>(type: "text", nullable: true),
                    ReferralNotes = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<int>(type: "integer", nullable: false)
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
                name: "Eyesights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VisitId = table.Column<int>(type: "integer", nullable: false),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VisitId = table.Column<int>(type: "integer", nullable: false),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConsultId = table.Column<int>(type: "integer", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConsultId = table.Column<int>(type: "integer", nullable: false),
                    MedicationBrandId = table.Column<int>(type: "integer", nullable: false),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferredFor = table.Column<string>(type: "text", nullable: false),
                    ReferralNotes = table.Column<string>(type: "text", nullable: true),
                    ReferralState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReferralOutcome = table.Column<string>(type: "text", nullable: true),
                    ConsultId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "ChildVitals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VitalsId = table.Column<int>(type: "integer", nullable: false),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VitalsId = table.Column<int>(type: "integer", nullable: false),
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
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    StockChangeId = table.Column<int>(type: "integer", nullable: false)
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
                name: "IX_ChildVitals_VitalsId",
                table: "ChildVitals",
                column: "VitalsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalFiles_PatientId_IsDeleted",
                table: "ClinicalFiles",
                columns: new[] { "PatientId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Consults_DoctorId",
                table: "Consults",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Consults_VisitId",
                table: "Consults",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_ConsultId",
                table: "Diagnoses",
                column: "ConsultId");

            migrationBuilder.CreateIndex(
                name: "IX_Eyesights_VisitId",
                table: "Eyesights",
                column: "VisitId",
                unique: true);

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
                name: "IX_Patients_IdentificationNumber",
                table: "Patients",
                column: "IdentificationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Name",
                table: "Patients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_VillageCodeId",
                table: "Patients",
                column: "VillageCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PubertyAssessments_VitalsId",
                table: "PubertyAssessments",
                column: "VitalsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ConsultId",
                table: "Referrals",
                column: "ConsultId");

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

            migrationBuilder.CreateIndex(
                name: "IX_StockChanges_StockId",
                table: "StockChanges",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_StockChanges_UserId",
                table: "StockChanges",
                column: "UserId");

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
                name: "Broadcasts");

            migrationBuilder.DropTable(
                name: "ChildVitals");

            migrationBuilder.DropTable(
                name: "ClinicalFiles");

            migrationBuilder.DropTable(
                name: "Diagnoses");

            migrationBuilder.DropTable(
                name: "Eyesights");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "PubertyAssessments");

            migrationBuilder.DropTable(
                name: "Referrals");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "StockChanges");

            migrationBuilder.DropTable(
                name: "Vitals");

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
