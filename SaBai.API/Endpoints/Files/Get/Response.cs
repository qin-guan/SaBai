using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Files.Get;

/// <summary>Clinical file metadata. Bytes stay on local disk.</summary>
public sealed record Response(
    int Id,
    int PatientId,
    string FileName,
    string StoredFileName,
    string Description,
    long SizeBytes,
    bool IsDeleted,
    DateTimeOffset CreatedAt)
{
    public static Response From(ClinicalFile file) =>
        new(file.Id, file.PatientId, file.FileName, file.StoredFileName, file.Description, file.SizeBytes, file.IsDeleted, file.CreatedAt);
}
