namespace SaBai.API.Endpoints.Consults.Delete.Delete;

/// <summary>Identifies a consult to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}
