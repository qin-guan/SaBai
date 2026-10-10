namespace SaBai.API.Endpoints.Vitals.ListByPatient.Get;

/// <summary>Identifies the patient whose vitals are listed.</summary>
public sealed record Request
{
    public int PatientId { get; init; }
}
