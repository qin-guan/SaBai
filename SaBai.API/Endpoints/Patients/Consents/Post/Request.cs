using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Consents.Post;

/// <param name="Id">Id for the new consent record, chosen by the client; generated when omitted. Resending with the same id returns the existing record.</param>
public sealed record Request(
    Guid PatientId,
    ConsentType ConsentType,
    bool Granted,
    string? Notes,
    Guid? Id = null
);
