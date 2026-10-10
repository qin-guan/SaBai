using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Diagnoses.List.Get;

/// <summary>Diagnosis row.</summary>
public sealed record Response(int Id, int ConsultId, string Details, DiagnosisCategory? Category);
