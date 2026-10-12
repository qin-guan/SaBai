namespace SaBai.API.Endpoints.Files.Create.Post;

/// <summary>Clinical file saved on local disk.</summary>
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
