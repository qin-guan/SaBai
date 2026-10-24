namespace SaBai.API.Endpoints.Patients.Delete.Delete;

/// <summary>Identifies a patient to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}
