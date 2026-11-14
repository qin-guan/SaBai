using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Consents.Post;

public sealed record Request(
    int PatientId,
    ConsentType ConsentType,
    bool Granted,
    string? Notes
);
