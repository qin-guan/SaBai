package sabai.api.endpoints.patients.delete

import io.swagger.v3.oas.annotations.Operation
import org.slf4j.LoggerFactory
import org.springframework.beans.factory.ObjectProvider
import org.springframework.http.HttpStatus
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.DeleteMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.ResponseStatus
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.PatientRepository
import sabai.api.data.VisitRepository
import sabai.api.data.findOrNotFound
import sabai.api.domain.ConflictException
import sabai.api.endpoints.patients.PatientFaceIdentity
import sabai.api.facerecognition.FaceStore
import sabai.api.storage.StorageSettings
import software.amazon.awssdk.services.s3.S3Client

@RestController
class Endpoint(
    private val patients: PatientRepository,
    private val visits: VisitRepository,
    private val s3: ObjectProvider<S3Client>,
    private val storage: StorageSettings,
    private val faces: FaceStore,
) {
    private val log = LoggerFactory.getLogger(javaClass)

    @Operation(summary = "Delete a patient")
    @StaffOnly
    @Transactional
    @DeleteMapping("/patients/{id}")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    fun handle(@PathVariable id: Int) {
        val patient = patients.findOrNotFound(id, "Patient was not found.")
        if (visits.existsByPatientId(patient.id)) throw ConflictException("Patient has visits and cannot be deleted.")

        patient.photoStoredFileName?.let(::deletePhoto)
        if (patient.latestFaceId != null) faces.deletePerson(PatientFaceIdentity.forPatient(patient.id))
        patients.delete(patient)
    }

    /** Best-effort removal of the stored photo; failures are logged, never thrown. */
    private fun deletePhoto(key: String) {
        runCatching { s3.getObject().deleteObject { it.bucket(storage.bucket).key(key) } }
            .onFailure { log.warn("Failed to delete patient photo object {}", key, it) }
    }
}
