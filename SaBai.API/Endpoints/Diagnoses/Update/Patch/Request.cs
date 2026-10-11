using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Diagnoses.Update.Patch;

/// <summary>Partial diagnosis update. Omitted fields are left unchanged.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public string? Details { get; init; }

    public DiagnosisCategory? Category { get; init; }
}
