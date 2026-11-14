using SaBai.API.Domain;

namespace SaBai.API.Endpoints.PatientConsult.Get;

/// <summary>Consult workspace for one visit. Consults are ordered by date, then id.</summary>
public sealed record Response(
    PatientItem Patient,
    VitalsItem? Vitals,
    DateTimeOffset VisitDate,
    IReadOnlyList<ConsultItem> Consults,
    IReadOnlyList<PrescriptionItem> Prescriptions
);

/// <summary>Patient identity shown on a visit chart.</summary>
public sealed record PatientItem(
    Guid Id,
    string Name,
    string? IdentificationNumber,
    string? ContactNo,
    Gender Gender,
    DateTimeOffset DateOfBirth,
    string DrugAllergy,
    bool HasPoorCard,
    bool HasBS2Card,
    bool HasSabaiCard,
    bool ToGetReport,
    Guid? VillageCodeId,
    string? VillageCode,
    string DisplayId,
    string FilterString,
    DateTimeOffset? LastVisitDate,
    Guid? LastVisitId
);

/// <summary>Vitals recorded for the visit, or null when none exist.</summary>
public sealed record VitalsItem(
    Guid Id,
    Guid VisitId,
    decimal? Height,
    decimal? Weight,
    decimal? Temperature,
    int? Systolic,
    int? Diastolic,
    int? HeartRate,
    decimal? HemocueCount,
    bool? DiabetesMellitus,
    string? UrineTest,
    decimal? BloodGlucoseNonFasting,
    decimal? BloodGlucoseFasting,
    decimal? Hba1c,
    string? Others
);

/// <summary>Consult summary ordered by date then id, including the doctor nickname.</summary>
public sealed record ConsultItem(
    Guid Id,
    DateTimeOffset Date,
    string DoctorNickname,
    string? ReferredFor
);

/// <summary>Prescription on a consult of this visit. Notes are the order remarks.</summary>
public sealed record PrescriptionItem(
    Guid ConsultId,
    DateTimeOffset VisitDate,
    string MedicationName,
    int Quantity,
    string? Notes,
    OrderStatus Status
);
