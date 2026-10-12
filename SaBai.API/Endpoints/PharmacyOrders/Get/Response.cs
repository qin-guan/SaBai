using SaBai.API.Domain;

namespace SaBai.API.Endpoints.PharmacyOrders.Get;

/// <summary>Pending pharmacy work grouped by patient.</summary>
public sealed record Response(PatientHeader Patient, IReadOnlyList<VisitBundle> Visits);

/// <summary>Patient header on the pharmacy queue.</summary>
public sealed record PatientHeader(string DisplayId, string Name, string? VillageCode);

/// <summary>One visit's pending orders and diagnoses.</summary>
public sealed record VisitBundle(
    int VisitId,
    DateTimeOffset VisitDate,
    IReadOnlyList<PharmacyOrderItem> Orders,
    IReadOnlyList<DiagnosisItem> Diagnoses
);

/// <summary>A pending medication order waiting to be dispensed.</summary>
public sealed record PharmacyOrderItem(
    int OrderId,
    string MedicationName,
    int Quantity,
    bool IsLowStock,
    string DosageInstructions
);

/// <summary>Diagnosis recorded on one of the visit's consults.</summary>
public sealed record DiagnosisItem(DiagnosisCategory? Category, string Details);
