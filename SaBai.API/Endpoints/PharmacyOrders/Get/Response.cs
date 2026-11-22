using SaBai.API.Domain;
using SaBai.API.Endpoints.Orders;

namespace SaBai.API.Endpoints.PharmacyOrders.Get;

/// <summary>Pending pharmacy work grouped by patient.</summary>
public sealed record Response(PatientHeader Patient, IReadOnlyList<VisitBundle> Visits);

/// <summary>Patient header on the pharmacy queue.</summary>
public sealed record PatientHeader(string DisplayId, string Name, string? VillageCode);

/// <summary>One visit's pending orders and diagnoses.</summary>
public sealed record VisitBundle(
    Guid VisitId,
    DateTimeOffset VisitDate,
    IReadOnlyList<PharmacyOrderItem> Orders,
    IReadOnlyList<DiagnosisItem> Diagnoses
);

/// <summary>
/// A pending medication order waiting to be dispensed. <c>MedicationName</c> is the active
/// ingredient; <c>Allocations</c> lists the brand and batch (with its location) to take each part
/// from. <c>IsLowStock</c>: the ingredient's available (unreserved) active stock is below its
/// <c>FallBelow</c>.
/// </summary>
public sealed record PharmacyOrderItem(
    Guid OrderId,
    string MedicationName,
    int Quantity,
    bool IsLowStock,
    string DosageInstructions,
    IReadOnlyList<AllocationDetails> Allocations
);

/// <summary>Diagnosis recorded on one of the visit's consults.</summary>
public sealed record DiagnosisItem(DiagnosisCategory? Category, string Details);
