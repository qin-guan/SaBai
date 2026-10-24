using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Diagnoses.Update.Patch;

/// <summary>Diagnosis after a partial update.</summary>
public sealed record Response(int Id, int ConsultId, string Details, DiagnosisCategory? Category);
