using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Consults.Create.Post;

/// <summary>New consult, its diagnoses, and any prescriptions to record together.</summary>
public sealed record Request
{
    public int VisitId { get; init; }

    public string? PastMedicalHistory { get; init; }

    public string? Consultation { get; init; }

    public string? TreatmentPlan { get; init; }

    public string? ReferredFor { get; init; }

    public string? ReferralNotes { get; init; }

    public string? Remarks { get; init; }

    public IReadOnlyList<DiagnosisInput> Diagnoses { get; init; } = [];

    public IReadOnlyList<OrderInput> Orders { get; init; } = [];
}

/// <summary>Diagnosis captured with a new consult.</summary>
public sealed record DiagnosisInput
{
    public string Details { get; init; } = "";

    public DiagnosisCategory? Category { get; init; }
}

/// <summary>Prescription inserted with a new consult. Status is always pending.</summary>
public sealed record OrderInput
{
    public int MedicationBrandId { get; init; }

    public int Quantity { get; init; }

    public string? DosageInstructions { get; init; }

    public string? Remarks { get; init; }
}
