using FastEndpoints;
using SaBai.API.Endpoints.Patients;

namespace SaBai.API.Endpoints.Persons;

public static class PersonId
{
    public const int MaxLength = 128;

    public static void Validate(IEndpoint endpoint, string? personId)
    {
        if (string.IsNullOrWhiteSpace(personId) || personId.Length > MaxLength)
            endpoint.ValidationFailures.Add(
                new("personId", $"PersonId must be 1-{MaxLength} characters.")
            );
        // Patient faces are managed through /patients/{id}/faces, which enforces biometric consent.
        else if (personId.StartsWith(PatientFaceIdentity.Prefix, StringComparison.OrdinalIgnoreCase))
            endpoint.ValidationFailures.Add(
                new(
                    "personId",
                    $"PersonId must not start with '{PatientFaceIdentity.Prefix}'; use /patients/{{id}}/faces."
                )
            );
    }
}
