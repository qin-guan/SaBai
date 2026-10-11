namespace SaBai.API.Endpoints.Patients.SearchByFace.Post;

/// <summary>A patient that matched the search photo, with the best similarity seen.</summary>
public sealed record PatientFaceMatch(
    int Id,
    string Name,
    string DisplayId,
    string? VillageCode,
    string? VillageColorHex,
    bool HasPhoto,
    float Similarity);

public sealed record Response(IReadOnlyList<PatientFaceMatch> Matches);
