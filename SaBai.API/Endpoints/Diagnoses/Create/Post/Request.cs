using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Diagnoses.Create.Post;

/// <summary>Diagnosis to add to an existing consult.</summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public Guid ConsultId { get; init; }

    public string Details { get; init; } = "";

    public DiagnosisCategory? Category { get; init; }
}
