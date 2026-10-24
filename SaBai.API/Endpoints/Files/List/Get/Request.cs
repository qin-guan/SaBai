namespace SaBai.API.Endpoints.Files.List.Get;

/// <summary>File list filters. Deleted defaults to non-deleted files.</summary>
public sealed record Request
{
    public int? PatientId { get; init; }

    public string? Deleted { get; init; }
}
