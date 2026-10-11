namespace SaBai.API.Domain;

public sealed class Patient
{
    public int Id { get; set; }

    public int? VillageCodeId { get; set; }

    public VillageCode? VillageCode { get; set; }

    public required string Name { get; set; }

    public string? IdentificationNumber { get; set; }

    public string? ContactNo { get; set; }

    public Gender Gender { get; set; }

    public DateTimeOffset DateOfBirth { get; set; }

    public required string DrugAllergy { get; set; }

    public bool HasPoorCard { get; set; }

    public bool HasBS2Card { get; set; }

    public bool HasSabaiCard { get; set; }

    public bool ToGetReport { get; set; }

    /// <summary>RustFS object key of the patient's profile photo, or null when none is set.
    /// Mapped to the legacy <c>PatientImagePublicId</c> column.</summary>
    public string? PhotoStoredFileName { get; set; }

    /// <summary>Qdrant face id most recently enrolled for this patient, or null.
    /// Used only as a "has a face" flag; all faces live in the face store.
    /// Mapped to the legacy <c>RekognitionFaceId</c> column.</summary>
    public string? LatestFaceId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Visit> Visits { get; set; } = [];

    public ICollection<ClinicalFile> Files { get; set; } = [];

    public static string FormatDisplayId(string? villageCode, int id) =>
        string.IsNullOrWhiteSpace(villageCode) ? id.ToString("D4") : $"{villageCode}{id:0000}";

    public static string FormatFilterString(string? villageCode, int id, string? contactNo, string name)
    {
        var display = FormatDisplayId(villageCode, id);
        var compact = string.IsNullOrWhiteSpace(villageCode) ? id.ToString() : $"{villageCode}{id}";
        return $"{display} {compact} {contactNo} {name}";
    }
}
