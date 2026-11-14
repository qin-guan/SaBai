namespace SaBai.API.Domain;

/// <summary>
/// A file kept in the RustFS bucket. Patient documents, dental images and any other upload
/// share this table; the bytes live in the bucket under <see cref="StoredFileName"/>.
/// </summary>
public sealed class StoredFile : ISyncEntity
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

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The patient the file belongs to. Null for files that are not about a patient.</summary>
    public Guid? PatientId { get; set; }

    public Patient? Patient { get; set; }

    public required string FileName { get; set; }

    public required string StoredFileName { get; set; }

    public required string Description { get; set; }

    public long SizeBytes { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
