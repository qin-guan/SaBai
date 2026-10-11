namespace SaBai.API.Domain;

public sealed class ClinicalFile
{
    public const int MaxBytes = 26_214_400;

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
