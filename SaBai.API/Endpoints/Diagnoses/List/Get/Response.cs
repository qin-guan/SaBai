using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Diagnoses.List.Get;

/// <summary>Diagnosis row.</summary>
public sealed record Response(Guid Id, Guid ConsultId, string Details, DiagnosisCategory? Category);
