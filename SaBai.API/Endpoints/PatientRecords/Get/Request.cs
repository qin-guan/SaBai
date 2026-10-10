namespace SaBai.API.Endpoints.PatientRecords.Get;

/// <summary>Visit whose chart should be loaded. Must be a positive integer.</summary>
public sealed record Request
{
    public int VisitId { get; init; }
}
