using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Consults.Create.Post;

/// <summary>New consult, its diagnoses, and any prescriptions to record together.</summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public Guid VisitId { get; init; }

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
    /// <summary>Id for the new diagnosis, chosen by the client; generated when omitted.</summary>
    public Guid? Id { get; init; }

    public string Details { get; init; } = "";

    public DiagnosisCategory? Category { get; init; }
}

/// <summary>Prescription inserted with a new consult. Status is always pending.</summary>
public sealed record OrderInput
{
    /// <summary>Id for the new order, chosen by the client; generated when omitted.</summary>
    public Guid? Id { get; init; }

    public Guid MedicationBrandId { get; init; }

    public int Quantity { get; init; }

    public string? DosageInstructions { get; init; }

    public string? Remarks { get; init; }
}
