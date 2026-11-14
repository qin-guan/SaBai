using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Diagnoses.Get;

/// <summary>Diagnosis row.</summary>
public sealed record Response(Guid Id, Guid ConsultId, string Details, DiagnosisCategory? Category)
{
    public static Response From(Diagnosis diagnosis) =>
        new(diagnosis.Id, diagnosis.ConsultId, diagnosis.Details, diagnosis.Category);
}
