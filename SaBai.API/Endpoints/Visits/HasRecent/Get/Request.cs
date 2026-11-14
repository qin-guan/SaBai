namespace SaBai.API.Endpoints.Visits.HasRecent.Get;

/// <summary>Identifies the patient to check for a just-created visit.</summary>
public sealed record Request
{
    public Guid PatientId { get; init; }
}
