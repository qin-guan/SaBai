using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Diagnoses.Update.Patch;

/// <summary>Diagnosis after a partial update.</summary>
public sealed record Response(Guid Id, Guid ConsultId, string Details, DiagnosisCategory? Category);
