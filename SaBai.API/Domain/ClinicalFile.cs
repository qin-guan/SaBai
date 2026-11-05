namespace SaBai.API.Domain;

public sealed class ClinicalFile
{
    public const int MaxBytes = 26_214_400;

    /// <summary>Content types a clinical file may be uploaded as: PDF, JPEG, PNG and DOCX.</summary>
    public static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    public int Id { get; set; }

    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public required string FileName { get; set; }

    public required string StoredFileName { get; set; }

    public required string Description { get; set; }

    public long SizeBytes { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
