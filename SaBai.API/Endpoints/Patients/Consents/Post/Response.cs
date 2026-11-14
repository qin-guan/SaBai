using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Consents.Post;

public sealed record Response(
    int Id,
    int PatientId,
    ConsentType ConsentType,
    bool Granted,
    DateTimeOffset RecordedAt,
    DateTimeOffset? RevokedAt
);
