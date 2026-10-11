namespace SaBai.API.Endpoints.PatientConsult.Get;

/// <summary>Visit whose consult workspace should be loaded. Must be a positive integer.</summary>
public sealed record Request
{
    public int VisitId { get; init; }
}
