namespace SaBai.API.Endpoints.Files.Restore.Post;

/// <summary>Clinical file after it is restored from the deleted list.</summary>
public sealed record Response(
    Guid Id,
    Guid? PatientId,
    string FileName,
    string StoredFileName,
    string Description,
    long SizeBytes,
    bool IsDeleted,
    DateTimeOffset CreatedAt
);
