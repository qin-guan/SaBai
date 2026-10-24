namespace SaBai.API.Endpoints.Files.Update.Patch;

/// <summary>Partial clinical file metadata update. Does not call an external store.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public string? FileName { get; init; }

    public string? Description { get; init; }
}
