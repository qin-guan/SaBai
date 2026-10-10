namespace SaBai.API.Endpoints.Files.Delete.Delete;

/// <summary>Identifies the clinical file to soft-delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}
