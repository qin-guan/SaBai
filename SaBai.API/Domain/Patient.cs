namespace SaBai.API.Domain;

public sealed class Patient : ITrackedEntity
{
    /// <summary>UUIDv7 key. Created by the client (possibly offline) or by the server.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    /// <summary>
    /// Human-readable patient number printed on cards (shown as <c>VillageCode + Number:0000</c>).
    /// Assigned by Postgres (identity) when the row is first written on the server, so a patient
    /// registered offline has no number until its queued registration reaches the server.
    /// Patients from before the move to UUID keys keep their old integer id here.
    /// </summary>
    public int Number { get; set; }

    public Guid? VillageCodeId { get; set; }

    public VillageCode? VillageCode { get; set; }

    [PhiData]
    public required string Name { get; set; }

    [PhiData]
    public string? IdentificationNumber { get; set; }

    [PhiData]
    public string? ContactNo { get; set; }

    public Gender Gender { get; set; }

    [PhiData]
    public DateTimeOffset DateOfBirth { get; set; }

    [PhiData]
    public required string DrugAllergy { get; set; }

    public bool HasPoorCard { get; set; }

    public bool HasBS2Card { get; set; }

    public bool HasSabaiCard { get; set; }

    public bool ToGetReport { get; set; }

    /// <summary>RustFS object key of the patient's profile photo, or null when none is set.
    /// Mapped to the legacy <c>PatientImagePublicId</c> column.</summary>
    [PhiData]
    public string? PhotoStoredFileName { get; set; }

    /// <summary>Qdrant face id most recently enrolled for this patient, or null.
    /// Used only as a "has a face" flag; all faces live in the face store.
    /// Mapped to the legacy <c>RekognitionFaceId</c> column.</summary>
    [PhiData]
    public string? LatestFaceId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Visit> Visits { get; set; } = [];

    public ICollection<StoredFile> Files { get; set; } = [];

    public ICollection<PatientTag> PatientTags { get; set; } = [];

    public static string FormatDisplayId(string? villageCode, int number) =>
        string.IsNullOrWhiteSpace(villageCode) ? number.ToString("D4") : $"{villageCode}{number:0000}";

    public static string FormatFilterString(
        string? villageCode,
        int number,
        string? contactNo,
        string name
    )
    {
        var display = FormatDisplayId(villageCode, number);
        var compact = string.IsNullOrWhiteSpace(villageCode) ? number.ToString() : $"{villageCode}{number}";
        return $"{display} {compact} {contactNo} {name}";
    }
}
