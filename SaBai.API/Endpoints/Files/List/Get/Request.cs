namespace SaBai.API.Endpoints.Files.List.Get;

/// <summary>File list filters. Deleted defaults to non-deleted files.</summary>
public sealed record Request
{
    public Guid? PatientId { get; init; }

    public string? Deleted { get; init; }
}
