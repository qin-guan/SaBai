namespace SaBai.API.Endpoints.Patients.Disclosures.Post;

/// <param name="Id">Id for the new disclosure record, chosen by the client; generated when omitted. Resending with the same id returns the existing record.</param>
public sealed record Request(
    Guid PatientId,
    string Recipient,
    string Purpose,
    string? Description,
    Guid? Id = null
);
