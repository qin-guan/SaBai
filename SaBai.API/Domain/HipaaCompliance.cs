namespace SaBai.API.Domain;

public enum ConsentType
{
    NoticeOfPrivacyPractices,
    BiometricFaceRecognition,
    ClinicalDataSharing,
}

/// <summary>
/// Tracks HIPAA Privacy Rule authorizations and biometric consent per patient.
/// </summary>
public sealed class PatientConsent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public ConsentType ConsentType { get; set; }

    public bool Granted { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid RecordedByUserId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Tracks external disclosures of PHI for HIPAA §164.528 Accounting of Disclosures.
/// </summary>
public sealed class DisclosureLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public required string Recipient { get; set; }

    public required string Purpose { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset DisclosedAt { get; set; }

    public Guid DisclosedByUserId { get; set; }
}
