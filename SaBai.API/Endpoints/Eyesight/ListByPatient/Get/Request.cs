namespace SaBai.API.Endpoints.Eyesight.ListByPatient.Get;

/// <summary>Identifies the patient whose eyesight records are listed.</summary>
public sealed record Request
{
    public int PatientId { get; init; }
}
