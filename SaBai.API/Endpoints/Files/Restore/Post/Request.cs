namespace SaBai.API.Endpoints.Files.Restore.Post;

/// <summary>Identifies the clinical file to restore.</summary>
public sealed record Request
{
    public int Id { get; init; }
}
