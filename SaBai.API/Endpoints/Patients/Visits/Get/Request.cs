namespace SaBai.API.Endpoints.Patients.Visits.Get;

/// <summary>Identifies the patient whose visits are listed.</summary>
public sealed record Request
{
    public Guid PatientId { get; init; }
}
