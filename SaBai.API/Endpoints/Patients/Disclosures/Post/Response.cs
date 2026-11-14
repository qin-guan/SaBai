namespace SaBai.API.Endpoints.Patients.Disclosures.Post;

public sealed record Response(
    int Id,
    int PatientId,
    string Recipient,
    string Purpose,
    DateTimeOffset DisclosedAt
);
