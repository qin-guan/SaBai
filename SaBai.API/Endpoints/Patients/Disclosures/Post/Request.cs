namespace SaBai.API.Endpoints.Patients.Disclosures.Post;

public sealed record Request(int PatientId, string Recipient, string Purpose, string? Description);
