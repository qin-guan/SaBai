namespace SaBai.API.Endpoints.Eyesight.Delete;

/// <summary>Identifies an eyesight record to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}
