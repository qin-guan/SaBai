package sabai.api.endpoints.patients.update

import io.swagger.v3.oas.annotations.Operation
import jakarta.validation.Valid
import jakarta.validation.constraints.Positive
import jakarta.validation.constraints.Size
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PatchMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.PatientRepository
import sabai.api.data.VillageCodeRepository
import sabai.api.data.findOrNotFound
import sabai.api.domain.ConflictException
import sabai.api.domain.Gender
import sabai.api.endpoints.patients.PatientReader
import sabai.api.endpoints.patients.PatientResponse
import sabai.api.web.NullOrNotBlank
import sabai.api.web.trimToNull
import java.time.OffsetDateTime

/** Partial patient update. Image fields are not accepted. */
data class Request(
    @NullOrNotBlank @Size(max = 255) val name: String? = null,
    @Size(max = 255) val identificationNumber: String? = null,
    @Size(max = 255) val contactNo: String? = null,
    val gender: Gender? = null,
    val dateOfBirth: OffsetDateTime? = null,
    @NullOrNotBlank val drugAllergy: String? = null,
    val hasPoorCard: Boolean? = null,
    val hasBS2Card: Boolean? = null,
    val hasSabaiCard: Boolean? = null,
    val toGetReport: Boolean? = null,
    @Positive val villageCodeId: Int? = null,
)

@RestController
class Endpoint(
    private val patients: PatientRepository,
    private val villageCodes: VillageCodeRepository,
    private val reader: PatientReader,
) {
    @Operation(summary = "Update a patient")
    @StaffOnly
    @Transactional
    @PatchMapping("/patients/{id}")
    fun handle(@PathVariable id: Int, @Valid @RequestBody req: Request): PatientResponse {
        val patient = patients.findOrNotFound(id, "Patient was not found.")

        req.name?.let { patient.name = it.trim() }
        req.identificationNumber?.let {
            val value = it.trimToNull()
            if (value != null && patients.existsByIdentificationNumberAndIdNot(value, patient.id)) {
                throw ConflictException("Another patient already has this identification number.")
            }
            patient.identificationNumber = value
        }
        req.contactNo?.let { patient.contactNo = it.trimToNull() }
        req.gender?.let { patient.gender = it }
        req.dateOfBirth?.let { patient.dateOfBirth = it }
        req.drugAllergy?.let { patient.drugAllergy = it.trim() }
        req.hasPoorCard?.let { patient.hasPoorCard = it }
        req.hasBS2Card?.let { patient.hasBS2Card = it }
        req.hasSabaiCard?.let { patient.hasSabaiCard = it }
        req.toGetReport?.let { patient.toGetReport = it }
        req.villageCodeId?.let { patient.villageCode = villageCodes.findOrNotFound(it, "Village code was not found.") }

        patients.flush() // write now so the read below sees the change
        return reader.get(patient.id)
    }
}
