using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Diagnoses.Create.Post;

/// <summary>Diagnosis just added to a consult.</summary>
public sealed record Response(int Id, int ConsultId, string Details, DiagnosisCategory? Category);
