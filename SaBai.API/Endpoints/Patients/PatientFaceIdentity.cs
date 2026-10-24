namespace SaBai.API.Endpoints.Patients;

/// <summary>
/// Maps a patient to the free-form <c>personId</c> used by the Qdrant face store.
/// Faces for patients are namespaced (<c>patient:42</c>) so they can never collide
/// with identities enrolled through the generic <c>/persons</c> endpoints.
/// </summary>
public static class PatientFaceIdentity
{
    private const string Prefix = "patient:";

    public static string ForPatient(int patientId) => $"{Prefix}{patientId}";

    public static bool TryParse(string? personId, out int patientId)
    {
        patientId = 0;
        return personId is not null
            && personId.StartsWith(Prefix, StringComparison.Ordinal)
            && int.TryParse(personId.AsSpan(Prefix.Length), out patientId);
    }
}
