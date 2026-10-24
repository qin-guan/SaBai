namespace SaBai.API.Endpoints.Patients.Get;

/// <summary>Identifies a patient.</summary>
public sealed record Request
{
    public int Id { get; init; }
}
