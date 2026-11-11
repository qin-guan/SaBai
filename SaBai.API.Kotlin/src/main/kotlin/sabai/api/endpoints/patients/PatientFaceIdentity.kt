package sabai.api.endpoints.patients

/**
 * Maps a patient to the `personId` used by the Qdrant face store. Namespaced
 * (`patient:42`) so it never collides with identities enrolled through `/persons`.
 */
object PatientFaceIdentity {
    private const val PREFIX = "patient:"

    fun forPatient(patientId: Int) = "$PREFIX$patientId"

    /** The patient id, or null when [personId] is not a patient identity. (C#: `TryParse(..., out id)`.) */
    fun parse(personId: String?): Int? = personId?.takeIf { it.startsWith(PREFIX) }?.removePrefix(PREFIX)?.toIntOrNull()
}
