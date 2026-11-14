namespace SaBai.API.Endpoints.Patients.Disclosures.Post;

public sealed record Response(
    Guid Id,
    Guid PatientId,
    string Recipient,
    string Purpose,
    DateTimeOffset DisclosedAt
);
