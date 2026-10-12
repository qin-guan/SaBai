namespace SaBai.API.Endpoints.Files.Update.Patch;

/// <summary>Clinical file after a metadata update.</summary>
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
