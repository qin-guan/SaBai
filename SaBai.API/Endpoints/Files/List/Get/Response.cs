namespace SaBai.API.Endpoints.Files.List.Get;

/// <summary>Clinical file metadata in a flat list.</summary>
public sealed record Response(
    int Id,
    int PatientId,
    string FileName,
    string StoredFileName,
    string Description,
    long SizeBytes,
    bool IsDeleted,
    DateTimeOffset CreatedAt
);
