namespace SaBai.Import.Upstream;

// Shapes of the upstream query results (the "json" part of each superjson payload), named after
// the select lists in upstream's routers. Dates arrive as ISO-8601 strings; Postgres numeric
// columns arrive as strings (Drizzle returns numeric as string).

public sealed record UpVillageCode(
    int Id,
    string Code,
    string Name,
    string ColorHex,
    bool IsVisible,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record UpPatient(
    int Id,
    string Name,
    string? IdentificationNumber,
    string? ContactNo,
    string Gender,
    DateTimeOffset DateOfBirth,
    bool HasPoorCard,
    bool HasBS2Card,
    string DrugAllergy,
    bool HasSabaiCard,
    string? PatientImagePublicId,
    string? RekognitionFaceId,
    string? PatientImageUrl
);

public sealed record UpVisit(int Id, DateTimeOffset Date, int PatientId, int VillageCodeId);

public sealed record UpVitals(
    int Id,
    string? Height,
    string? Weight,
    string? Temperature,
    int? Systolic,
    int? Diastolic,
    int? HeartRate,
    string? HemocueCount,
    bool? DiabetesMellitus,
    string? UrineTest,
    string? BloodGlucoseNonFasting,
    string? BloodGlucoseFasting,
    string? Hba1c,
    string? Others,
    int VisitId
);

public sealed record UpPuberty(
    int Id,
    bool? Pubarche,
    int? PubarcheAge,
    bool? Thelarche,
    int? ThelarcheAge,
    bool? Menarche,
    int? MenarcheAge,
    bool? VoiceChange,
    int? VoiceChangeAge,
    bool? TesticularGrowth,
    int? TesticularGrowthAge,
    string? AdditionalNotes,
    int VitalId
);

public sealed record UpEyesight(
    int Id,
    int VisitId,
    string? LeftEyeDegree,
    string? RightEyeDegree,
    string? LeftEyePinhole,
    string? RightEyePinhole,
    string? LeftAstigmatism,
    string? RightAstigmatism,
    string? Comments,
    string? LeftPrescribedGlassesDegree,
    string? RightPrescribedGlassesDegree
);

public sealed record UpDiagnosis(int Id, string Details, string? Category, int ConsultId);

public sealed record UpConsult(
    int Id,
    DateTimeOffset Date,
    string? PastMedicalHistory,
    string? Consultation,
    string? TreatmentPlan,
    string? Remarks,
    Guid DoctorId,
    int VisitId,
    IReadOnlyList<UpDiagnosis>? Diagnoses
);

public sealed record UpReferral(
    int Id,
    string ReferredFor,
    string? ReferralNotes,
    string ReferralState,
    string? ReferralOutcome,
    int ConsultId,
    DateTimeOffset CreatedAt
);

public sealed record UpReferralPartner(
    Guid Id,
    string Name,
    string? Remarks,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record UpIngredient(int Id, string Name, string UnitOfMeasurement, int FallBelow, string? Remarks);

public sealed record UpBrand(int Id, string Name, int ActiveIngredientId, string? Remarks);

public sealed record UpStock(
    int Id,
    int MedicationBrandId,
    int Quantity,
    DateTimeOffset Expiry,
    string Location,
    string StockStatus,
    string? Remarks
);

public sealed record UpBroadcast(
    int Id,
    string Title,
    string Body,
    string Severity,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    int Attempts,
    string? SenderName
);

public sealed record UpFile(
    int Id,
    int? PatientId,
    string S3Key,
    string ContentType,
    string FileName,
    string Description,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    string FileUrl
);
