namespace SaBai.API.Endpoints.Files.Delete.Delete;

/// <summary>Clinical file after a soft delete.</summary>
public sealed record Response(
    int Id,
    int PatientId,
    string FileName,
    string StoredFileName,
    string Description,
    long SizeBytes,
    bool IsDeleted,
    DateTimeOffset CreatedAt);
