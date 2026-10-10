using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Diagnoses.Create.Post;

/// <summary>Diagnosis to add to an existing consult.</summary>
public sealed record Request
{
    public int ConsultId { get; init; }

    public string Details { get; init; } = "";

    public DiagnosisCategory? Category { get; init; }
}
