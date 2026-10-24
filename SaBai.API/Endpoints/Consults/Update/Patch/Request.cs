namespace SaBai.API.Endpoints.Consults.Update.Patch;

/// <summary>Partial text update. Omitted fields are left unchanged. Diagnoses and orders are not edited.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public string? PastMedicalHistory { get; init; }

    public string? Consultation { get; init; }

    public string? TreatmentPlan { get; init; }

    public string? ReferredFor { get; init; }

    public string? ReferralNotes { get; init; }

    public string? Remarks { get; init; }
}
