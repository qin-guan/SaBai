namespace SaBai.API.Endpoints.Patients.Disclosures.Post;

public sealed record Request(Guid PatientId, string Recipient, string Purpose, string? Description);
